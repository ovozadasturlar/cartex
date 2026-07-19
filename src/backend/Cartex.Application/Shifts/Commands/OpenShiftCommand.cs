using Cartex.Application.Common.Finance;
using Cartex.Application.Common.Messaging;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;

namespace Cartex.Application.Shifts.Commands;

public record OpenShiftCommand(decimal OpeningFloat, List<CurrencyAmountDto>? Floats = null) : ICommand<long>;

public sealed class OpenShiftCommandHandler(IApplicationDbContext db, ICurrentUser currentUser, ICurrencyService currency, IAuditService audit) : IRequestHandler<OpenShiftCommand, long>
{
    public async Task<long> Handle(OpenShiftCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");
        var branchId = currentUser.DefaultBranchId ?? throw new BusinessRuleException("Filial aniqlanmadi.");

        var baseCode = await currency.BaseAsync(cancellationToken);
        foreach (var row in request.Floats ?? [])
        {
            if (string.Equals(row.Currency, baseCode, StringComparison.OrdinalIgnoreCase))
                throw new BusinessRuleException("Bazaviy valyuta alohida qatorda yuborilmaydi.");
            await currency.EnsureAllowedAsync(row.Currency, cancellationToken);
        }

        var hasOpen = await db.Shifts.AnyAsync(s => s.UserId == userId && s.BranchId == branchId && s.Status == ShiftStatus.Open, cancellationToken);
        if (hasOpen)
            throw new BusinessRuleException("Ochiq smena allaqachon mavjud.");

        var shift = new Shift
        {
            BranchId = branchId,
            UserId = userId,
            OpenedAt = DateTime.UtcNow,
            OpeningFloat = request.OpeningFloat,
            Status = ShiftStatus.Open
        };

        foreach (var row in request.Floats ?? [])
            shift.CashRows.Add(new ShiftCash { Currency = row.Currency, OpeningFloat = row.Amount });

        db.Shifts.Add(shift);
        await db.SaveChangesAsync(cancellationToken);
        audit.Add("open", "shifts", shift.Id, new { request.OpeningFloat });
        await db.SaveChangesAsync(cancellationToken);
        return shift.Id;
    }
}

public sealed class OpenShiftCommandValidator : AbstractValidator<OpenShiftCommand>
{
    public OpenShiftCommandValidator()
    {
        RuleFor(x => x.OpeningFloat).GreaterThanOrEqualTo(0);
        RuleForEach(x => x.Floats).Must(f => f.Amount >= 0).When(x => x.Floats is not null);
    }
}
