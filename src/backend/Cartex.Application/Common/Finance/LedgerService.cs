using System.Linq.Expressions;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Common.Finance;

public interface ILedgerService
{
    Task<Account> BranchAccountAsync(long branchId, AccountType type, CancellationToken cancellationToken);
    Task<Account> CustomerAccountAsync(long customerId, AccountType type, CancellationToken cancellationToken);
    Task<Account?> FindCustomerAccountAsync(long customerId, AccountType type, CancellationToken cancellationToken);
    Transaction Post(OperationType type, decimal amount, Account? from, Account? to, long userId);
}

public sealed class LedgerService(IApplicationDbContext db) : ILedgerService
{
    public Task<Account> BranchAccountAsync(long branchId, AccountType type, CancellationToken cancellationToken) =>
        GetOrCreateAsync(a => a.BranchId == branchId && a.Type == type,
            () => new Account { BranchId = branchId, Type = type, Name = DefaultName(type) }, cancellationToken);

    public Task<Account> CustomerAccountAsync(long customerId, AccountType type, CancellationToken cancellationToken) =>
        GetOrCreateAsync(a => a.CustomerId == customerId && a.Type == type,
            () => new Account { CustomerId = customerId, Type = type, Name = DefaultName(type) }, cancellationToken);

    public async Task<Account?> FindCustomerAccountAsync(long customerId, AccountType type, CancellationToken cancellationToken) =>
        db.Accounts.Local.FirstOrDefault(a => a.CustomerId == customerId && a.Type == type)
        ?? await db.Accounts.FirstOrDefaultAsync(a => a.CustomerId == customerId && a.Type == type, cancellationToken);

    public Transaction Post(OperationType type, decimal amount, Account? from, Account? to, long userId)
    {
        if (from is not null) from.Balance -= amount;
        if (to is not null) to.Balance += amount;

        var transaction = new Transaction
        {
            FromAccount = from,
            ToAccount = to,
            Amount = amount,
            OperationType = type,
            UserId = userId
        };
        db.Transactions.Add(transaction);
        return transaction;
    }

    private async Task<Account> GetOrCreateAsync(Expression<Func<Account, bool>> predicate, Func<Account> factory, CancellationToken cancellationToken)
    {
        var existing = db.Accounts.Local.FirstOrDefault(predicate.Compile())
            ?? await db.Accounts.FirstOrDefaultAsync(predicate, cancellationToken);

        if (existing is not null)
            return existing;

        var account = factory();
        db.Accounts.Add(account);
        return account;
    }

    private static string DefaultName(AccountType type) => type switch
    {
        AccountType.Cash => "Naqd kassa",
        AccountType.Card => "Bank karta",
        AccountType.Bonus => "Bonus",
        AccountType.Debt => "Qarz",
        _ => type.ToString()
    };
}
