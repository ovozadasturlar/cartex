using Cartex.Application.Common.Messaging;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Application.Common.Models;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using Cartex.Domain.Enums;

namespace Cartex.Application.Shifts.Commands;

public record CloseShiftCommand(long ShiftId, decimal CountedCash, List<CurrencyAmountDto>? Counted = null) : ICommand<ZReportDto>;

public sealed class CloseShiftCommandHandler(IApplicationDbContext db, IAuditService audit) : IRequestHandler<CloseShiftCommand, ZReportDto>
{
    public async Task<ZReportDto> Handle(CloseShiftCommand request, CancellationToken cancellationToken)
    {
        var shift = await db.Shifts.FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken)
            ?? throw new NotFoundException("Shift not found.");

        if (shift.Status != ShiftStatus.Open)
            throw new BusinessRuleException("Smena ochiq emas.");

        var cashRows = await db.ShiftCashes.Where(c => c.ShiftId == shift.Id).ToListAsync(cancellationToken);
        foreach (var counted in request.Counted ?? [])
        {
            var row = cashRows.FirstOrDefault(c => c.Currency == counted.Currency);
            if (row is null)
            {
                row = new ShiftCash { ShiftId = shift.Id, Currency = counted.Currency };
                db.ShiftCashes.Add(row);
                cashRows.Add(row);
            }
            row.CountedCash = counted.Amount;
        }
        await db.SaveChangesAsync(cancellationToken);

        var report = await ShiftCalculator.ComputeAsync(db, shift, request.CountedCash, cancellationToken);

        shift.CountedCash = request.CountedCash;
        shift.ClosedAt = DateTime.UtcNow;
        shift.Status = ShiftStatus.Closed;

        audit.Add("close", "shifts", shift.Id, new { request.CountedCash, report.ExpectedCash });

        await db.SaveChangesAsync(cancellationToken);
        return report;
    }
}

public sealed class CloseShiftCommandValidator : AbstractValidator<CloseShiftCommand>
{
    public CloseShiftCommandValidator()
    {
        RuleFor(x => x.CountedCash).GreaterThanOrEqualTo(0);
    }
}
