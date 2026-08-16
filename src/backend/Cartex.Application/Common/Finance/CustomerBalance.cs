using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Common.Finance;

/// The customer's position as one number in base currency: positive means they owe the shop,
/// negative means the shop owes them. Debt and advance are separate accounts, and each can exist
/// per currency, so a single readable figure has to be derived rather than read off one row.
public static class CustomerBalance
{
    public static async Task<decimal> NetAsync(
        IApplicationDbContext db, ICurrencyService currency, long customerId, CancellationToken cancellationToken)
    {
        // Reads the tracked entities, so postings made in this unit of work are already counted.
        var accounts = await db.Accounts
            .Where(x => x.CustomerId == customerId
                && (x.Type == AccountType.Debt || x.Type == AccountType.CustomerAdvance)
                && x.Balance != 0)
            .ToListAsync(cancellationToken);

        var net = 0m;
        foreach (var account in accounts)
        {
            var rate = await currency.RateAsync(account.Currency, cancellationToken);
            var amount = Math.Round(account.Balance * rate, 2);
            net += account.Type == AccountType.Debt ? amount : -amount;
        }
        return net;
    }
}
