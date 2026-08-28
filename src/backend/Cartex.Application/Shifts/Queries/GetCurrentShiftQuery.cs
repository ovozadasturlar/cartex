using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Shared.Models.Shifts;

namespace Cartex.Application.Shifts.Queries;

public record GetCurrentShiftQuery : IRequest<CurrentShiftDto?>;

public sealed class GetCurrentShiftQueryHandler(IApplicationDbContext db, ICurrentUser currentUser) : IRequestHandler<GetCurrentShiftQuery, CurrentShiftDto?>
{
    public async Task<CurrentShiftDto?> Handle(GetCurrentShiftQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        var branchId = currentUser.DefaultBranchId;
        if (userId is null || branchId is null)
            return null;

        var shift = await db.Shifts.FirstOrDefaultAsync(
            s => s.UserId == userId && s.BranchId == branchId && s.Status == ShiftStatus.Open, cancellationToken);
        if (shift is null)
            return null;

        var report = await ShiftCalculator.ComputeAsync(db, shift, 0, cancellationToken);
        return new CurrentShiftDto(shift.Id, shift.OpenedAt, report.OpeningFloat,
            report.CashSales, report.CashReturns, report.PayIn, report.PayOut, report.DebtPayIn, report.SupplyPayOut, report.ExpectedCash)
        {
            CardSales = report.CardSales,
            CardReturns = report.CardReturns,
            BonusUsed = report.BonusUsed,
            NewDebtIssued = report.NewDebtIssued,
            SalesCount = report.SalesCount,
            Currencies = report.Currencies
        };
    }
}
