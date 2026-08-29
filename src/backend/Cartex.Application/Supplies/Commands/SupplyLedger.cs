using Cartex.Domain.Entities;

namespace Cartex.Application.Supplies.Commands;

internal static class SupplyLedger
{
    public static IEnumerable<long> AccountIds(IEnumerable<Transaction> transactions) =>
        transactions.SelectMany(x => new[] { x.FromAccountId, x.ToAccountId }).OfType<long>();
}
