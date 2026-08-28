using Cartex.Domain.Entities;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Common.Finance;

/// A ceiling decided from a balance has to be decided from a locked balance: two concurrent
/// debt sales that both read the same stale figure both pass the check and the customer ends
/// up over the limit. The rows are taken through the ledger lock so the established order
/// (shifts, documents, stocks, then accounts) is kept.
public static class CustomerAccounts
{
    public static async Task<List<Account>> LockAsync(
        IApplicationDbContext db, ILedgerService ledger, long customerId, CancellationToken cancellationToken)
    {
        var accounts = await db.Accounts
            .Where(x => x.CustomerId == customerId)
            .ToListAsync(cancellationToken);
        await ledger.LockAsync(accounts.Select(x => x.Id), cancellationToken);
        return accounts;
    }
}
