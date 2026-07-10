using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Shifts;

public record ZReportCurrencyDto(string Currency, decimal OpeningFloat, decimal CashSales, decimal CashReturns, decimal DebtPayIn, decimal SupplyPayOut, decimal ExpectedCash, decimal CountedCash, decimal Difference);

public record ZReportDto(long ShiftId, decimal OpeningFloat, decimal CashSales, decimal CashReturns, decimal PayIn, decimal PayOut, decimal DebtPayIn, decimal SupplyPayOut, decimal ExpectedCash, decimal CountedCash, decimal Difference)
{
    public List<ZReportCurrencyDto> Currencies { get; init; } = [];
}

public static class ShiftCalculator
{
    public static async Task<ZReportDto> ComputeAsync(IApplicationDbContext db, Shift shift, decimal countedCash, CancellationToken cancellationToken)
    {
        var baseCode = await db.Businesses.Select(b => b.Currency).FirstAsync(cancellationToken);
        var cashAccounts = await db.Accounts
            .Where(a => a.BranchId == shift.BranchId && a.Type == AccountType.Cash)
            .Select(a => new { a.Id, a.Currency })
            .ToListAsync(cancellationToken);

        var cashRows = await db.ShiftCashes.Where(c => c.ShiftId == shift.Id).ToListAsync(cancellationToken);
        var txns = await db.Transactions.Where(t => t.ShiftId == shift.Id).ToListAsync(cancellationToken);

        (decimal Sales, decimal Returns, decimal DebtIn, decimal SupplyOut, decimal ChangeOut) Terms(long? accountId)
        {
            var sales = txns.Where(t => t.OperationType == OperationType.Sale && t.ToAccountId == accountId).Sum(t => t.Amount);
            var returns = txns.Where(t => t.OperationType == OperationType.Sale && t.FromAccountId == accountId).Sum(t => t.Amount);
            var debtIn = txns.Where(t => t.OperationType == OperationType.DebtPay && t.ToAccountId == accountId).Sum(t => t.Amount);
            var supplyOut = txns.Where(t => (t.OperationType == OperationType.SupplyPay || t.OperationType == OperationType.DebtPay) && t.FromAccountId == accountId).Sum(t => t.Amount)
                - txns.Where(t => t.OperationType == OperationType.SupplyPay && t.ToAccountId == accountId).Sum(t => t.Amount);
            var changeOut = txns.Where(t => t.OperationType == OperationType.Change && t.FromAccountId == accountId).Sum(t => t.Amount);
            return (sales, returns, debtIn, supplyOut, changeOut);
        }

        var baseAccountId = cashAccounts.FirstOrDefault(a => a.Currency == baseCode)?.Id;
        var baseTerms = Terms(baseAccountId);
        var payIn = txns.Where(t => t.OperationType == OperationType.CashIn).Sum(t => t.Amount);
        var payOut = txns.Where(t => t.OperationType == OperationType.CashOut).Sum(t => t.Amount);

        var expected = shift.OpeningFloat + baseTerms.Sales - baseTerms.Returns + payIn - payOut
            + baseTerms.DebtIn - baseTerms.SupplyOut - baseTerms.ChangeOut;

        var currencies = new List<ZReportCurrencyDto>();
        foreach (var account in cashAccounts.Where(a => a.Currency != baseCode))
        {
            var row = cashRows.FirstOrDefault(c => c.Currency == account.Currency);
            var terms = Terms(account.Id);
            var opening = row?.OpeningFloat ?? 0;
            var counted = row?.CountedCash ?? 0;
            var expectedCcy = opening + terms.Sales - terms.Returns + terms.DebtIn - terms.SupplyOut - terms.ChangeOut;
            currencies.Add(new ZReportCurrencyDto(account.Currency, opening, terms.Sales, terms.Returns, terms.DebtIn, terms.SupplyOut, expectedCcy, counted, counted - expectedCcy));
        }

        return new ZReportDto(shift.Id, shift.OpeningFloat, baseTerms.Sales, baseTerms.Returns, payIn, payOut,
            baseTerms.DebtIn, baseTerms.SupplyOut, expected, countedCash, countedCash - expected)
        {
            Currencies = currencies
        };
    }
}
