using System.Linq.Expressions;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Common.Finance;

public interface ILedgerService
{
    Task<Account> BranchAccountAsync(long branchId, AccountType type, CancellationToken cancellationToken, string? currency = null);
    Task<Account> CustomerAccountAsync(long customerId, AccountType type, CancellationToken cancellationToken, string? currency = null);
    Task<Account?> FindCustomerAccountAsync(long customerId, AccountType type, CancellationToken cancellationToken, string? currency = null);
    Task<Account> SupplierAccountAsync(long supplierId, AccountType type, CancellationToken cancellationToken, string? currency = null);
    Task<Account?> FindSupplierAccountAsync(long supplierId, AccountType type, CancellationToken cancellationToken, string? currency = null);
    Transaction Post(OperationType type, decimal amount, Account? from, Account? to, long userId, long? shiftId = null, decimal rate = 1m);
}

public sealed class LedgerService(IApplicationDbContext db, ICurrencyService currency) : ILedgerService
{
    private readonly HashSet<long> _locked = [];

    private async Task LockAsync(Account account, CancellationToken cancellationToken)
    {
        if (account.Id <= 0 || !_locked.Add(account.Id))
            return;
        await db.Accounts.FromSqlInterpolated($"SELECT * FROM accounts WHERE id = {account.Id} FOR UPDATE").ToListAsync(cancellationToken);
        await db.ReloadAsync(account, cancellationToken);
    }

    public async Task<Account> BranchAccountAsync(long branchId, AccountType type, CancellationToken cancellationToken, string? currencyCode = null)
    {
        var (code, isBase) = await ResolveAsync(currencyCode, cancellationToken);
        return await GetOrCreateAsync(a => a.BranchId == branchId && a.Type == type && a.Currency == code,
            () => new Account { BranchId = branchId, Type = type, Currency = code, Name = DefaultName(type, code, isBase) }, cancellationToken);
    }

    public async Task<Account> CustomerAccountAsync(long customerId, AccountType type, CancellationToken cancellationToken, string? currencyCode = null)
    {
        var (code, isBase) = await ResolveAsync(currencyCode, cancellationToken);
        return await GetOrCreateAsync(a => a.CustomerId == customerId && a.Type == type && a.Currency == code,
            () => new Account { CustomerId = customerId, Type = type, Currency = code, Name = DefaultName(type, code, isBase) }, cancellationToken);
    }

    public async Task<Account?> FindCustomerAccountAsync(long customerId, AccountType type, CancellationToken cancellationToken, string? currencyCode = null)
    {
        var (code, _) = await ResolveAsync(currencyCode, cancellationToken);
        var account = db.Accounts.Local.FirstOrDefault(a => a.CustomerId == customerId && a.Type == type && a.Currency == code)
            ?? await db.Accounts.FirstOrDefaultAsync(a => a.CustomerId == customerId && a.Type == type && a.Currency == code, cancellationToken);
        if (account is not null)
            await LockAsync(account, cancellationToken);
        return account;
    }

    public async Task<Account> SupplierAccountAsync(long supplierId, AccountType type, CancellationToken cancellationToken, string? currencyCode = null)
    {
        var (code, isBase) = await ResolveAsync(currencyCode, cancellationToken);
        return await GetOrCreateAsync(a => a.SupplierId == supplierId && a.Type == type && a.Currency == code,
            () => new Account { SupplierId = supplierId, Type = type, Currency = code, Name = DefaultName(type, code, isBase) }, cancellationToken);
    }

    public async Task<Account?> FindSupplierAccountAsync(long supplierId, AccountType type, CancellationToken cancellationToken, string? currencyCode = null)
    {
        var (code, _) = await ResolveAsync(currencyCode, cancellationToken);
        var account = db.Accounts.Local.FirstOrDefault(a => a.SupplierId == supplierId && a.Type == type && a.Currency == code)
            ?? await db.Accounts.FirstOrDefaultAsync(a => a.SupplierId == supplierId && a.Type == type && a.Currency == code, cancellationToken);
        if (account is not null)
            await LockAsync(account, cancellationToken);
        return account;
    }

    public Transaction Post(OperationType type, decimal amount, Account? from, Account? to, long userId, long? shiftId = null, decimal rate = 1m)
    {
        if (from is not null && to is not null && from.Currency != to.Currency)
            throw new BusinessRuleException("Tranzaksiya hisoblari valyutasi mos emas.");

        if (from is not null) from.Balance -= amount;
        if (to is not null) to.Balance += amount;

        var transaction = new Transaction
        {
            FromAccount = from,
            ToAccount = to,
            Amount = amount,
            Currency = (from ?? to)?.Currency ?? "UZS",
            Rate = rate,
            OperationType = type,
            BranchId = from?.BranchId ?? to?.BranchId,
            UserId = userId,
            ShiftId = shiftId
        };
        db.Transactions.Add(transaction);
        return transaction;
    }

    private async Task<(string Code, bool IsBase)> ResolveAsync(string? code, CancellationToken cancellationToken)
    {
        var baseCode = await currency.BaseAsync(cancellationToken);
        return (code ?? baseCode, code is null || code == baseCode);
    }

    private async Task<Account> GetOrCreateAsync(Expression<Func<Account, bool>> predicate, Func<Account> factory, CancellationToken cancellationToken)
    {
        var existing = db.Accounts.Local.FirstOrDefault(predicate.Compile())
            ?? await db.Accounts.FirstOrDefaultAsync(predicate, cancellationToken);

        if (existing is not null)
        {
            await LockAsync(existing, cancellationToken);
            return existing;
        }

        var account = factory();
        db.Accounts.Add(account);
        return account;
    }

    private static string DefaultName(AccountType type, string currency, bool isBase)
    {
        var name = type switch
        {
            AccountType.Cash => "Naqd kassa",
            AccountType.Card => "Bank karta",
            AccountType.Transfer => "Mobil o'tkazma",
            AccountType.Bank => "Bank o'tkazmasi",
            AccountType.Bonus => "Bonus",
            AccountType.Debt => "Qarz",
            AccountType.CustomerAdvance => "Mijoz avansi",
            AccountType.RewardRecovery => "Bonus qaytaruv qarzi",
            _ => type.ToString()
        };
        return isBase ? name : $"{name} {currency}";
    }
}
