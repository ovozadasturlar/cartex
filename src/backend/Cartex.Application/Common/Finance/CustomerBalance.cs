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
        // Postings made in this unit of work count too, so the rows the ledger has only just
        // created are merged in: they are not in the database yet and no query would see them.
        var accounts = await db.Accounts
            .Where(x => x.CustomerId == customerId
                && (x.Type == AccountType.Debt || x.Type == AccountType.CustomerAdvance))
            .ToListAsync(cancellationToken);
        accounts.AddRange(db.Accounts.Local
            .Where(x => x.CustomerId == customerId
                && (x.Type == AccountType.Debt || x.Type == AccountType.CustomerAdvance))
            .Except(accounts));

        var net = 0m;
        foreach (var account in accounts.Where(x => x.Balance != 0))
        {
            var rate = await currency.RateAsync(account.Currency, cancellationToken);
            var amount = Math.Round(account.Balance * rate, 2);
            net += account.Type == AccountType.Debt ? amount : -amount;
        }
        return net;
    }
}
