using Cartex.Application.Common.Finance;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Application.Common.Models;
using Cartex.Application.Common.Shifts;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using Cartex.Domain.Enums;
using Cartex.Shared.Models.Common;
using Cartex.Shared.Models.Shifts;

namespace Cartex.Application.Shifts.Commands;

public record CloseShiftCommand(long ShiftId, decimal CountedCash, List<CurrencyAmountDto>? Counted = null) : ICommand<ZReportDto>;

public sealed class CloseShiftCommandHandler(IApplicationDbContext db, ICurrentUser currentUser, ICurrencyService currency, IShiftLock shiftLock, IAuditService audit) : IRequestHandler<CloseShiftCommand, ZReportDto>
{
    public async Task<ZReportDto> Handle(CloseShiftCommand request, CancellationToken cancellationToken)
    {
        var shift = await shiftLock.ByIdAsync(request.ShiftId, cancellationToken);
        if (shift is null || shift.BranchId != currentUser.DefaultBranchId)
            throw new NotFoundException("Shift not found.");

        if (shift.UserId != currentUser.UserId && !currentUser.HasPermission(AppPermissions.Shifts.CloseAll))
            throw new ForbiddenException("Boshqa kassirning smenasini yopishga ruxsat yo'q.");

        if (shift.Status != ShiftStatus.Open)
            throw new BusinessRuleException("Smena ochiq emas.");

        var baseCode = await currency.BaseAsync(cancellationToken);
        var cashRows = await db.ShiftCashes.Where(c => c.ShiftId == shift.Id).ToListAsync(cancellationToken);
        foreach (var counted in request.Counted ?? [])
        {
            if (string.Equals(counted.Currency, baseCode, StringComparison.OrdinalIgnoreCase))
                throw new BusinessRuleException("Bazaviy valyuta alohida qatorda yuborilmaydi.");
            var row = cashRows.FirstOrDefault(c => c.Currency == counted.Currency);
            if (row is null)
            {
                await currency.EnsureSalesAllowedAsync(counted.Currency, cancellationToken);
                row = new ShiftCash { ShiftId = shift.Id, Currency = counted.Currency };
                db.ShiftCashes.Add(row);
                cashRows.Add(row);
            }
            row.CountedCash = counted.Amount;
        }
        var baseRow = cashRows.FirstOrDefault(c => c.Currency == baseCode);
        if (baseRow is null)
        {
            baseRow = new ShiftCash { ShiftId = shift.Id, Currency = baseCode };
            db.ShiftCashes.Add(baseRow);
        }
        baseRow.CountedCash = request.CountedCash;
        await db.SaveChangesAsync(cancellationToken);

        var report = await ShiftCalculator.ComputeAsync(db, shift, request.CountedCash, cancellationToken);

        shift.ClosedAt = DateTime.UtcNow;
        shift.Status = ShiftStatus.Closed;

        audit.Add(shift.UserId != currentUser.UserId ? "forceClose" : "close", "shifts", shift.Id, new { request.CountedCash, report.ExpectedCash });

        await db.SaveChangesAsync(cancellationToken);
        return report;
    }
}

public sealed class CloseShiftCommandValidator : AbstractValidator<CloseShiftCommand>
{
    public CloseShiftCommandValidator()
    {
        RuleFor(x => x.CountedCash).GreaterThanOrEqualTo(0);
        RuleForEach(x => x.Counted).Must(c => c.Amount >= 0).When(x => x.Counted is not null);
    }
}
