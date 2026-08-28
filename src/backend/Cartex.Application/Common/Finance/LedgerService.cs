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
    Task<Account> AccountAsync(long accountId, CancellationToken cancellationToken);
    Task LockAsync(IEnumerable<long> accountIds, CancellationToken cancellationToken);
    Task<Transaction> PostAsync(OperationType type, decimal amount, Account? from, Account? to, long userId,
        CancellationToken cancellationToken, long? shiftId = null, decimal rate = 1m);
}

public sealed class LedgerService(IApplicationDbContext db, ICurrencyService currency) : ILedgerService
{
    private readonly TransactionScoped<HashSet<long>> _locked = new(db, () => []);

    public async Task LockAsync(IEnumerable<long> accountIds, CancellationToken cancellationToken)
    {
        var ids = accountIds.Where(id => id > 0 && _locked.Value.Add(id)).Order().ToArray();
        if (ids.Length == 0)
            return;
        var rows = await db.Accounts
            .FromSqlInterpolated($"SELECT * FROM accounts WHERE id = ANY({ids}) ORDER BY id FOR UPDATE")
            .ToListAsync(cancellationToken);
        foreach (var row in rows)
            await db.ReloadAsync(row, cancellationToken);
    }

    public async Task<Account> AccountAsync(long accountId, CancellationToken cancellationToken) =>
        db.Accounts.Local.FirstOrDefault(a => a.Id == accountId)
            ?? await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken)
            ?? throw new NotFoundException("Account not found.", "account_not_found");

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
            await LockAsync([account.Id], cancellationToken);
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
            await LockAsync([account.Id], cancellationToken);
        return account;
    }

    public async Task<Transaction> PostAsync(OperationType type, decimal amount, Account? from, Account? to, long userId,
        CancellationToken cancellationToken, long? shiftId = null, decimal rate = 1m)
    {
        if (from is not null && to is not null && from.Currency != to.Currency)
            throw new BusinessRuleException("Tranzaksiya hisoblari valyutasi mos emas.");

        await LockAsync([from?.Id ?? 0, to?.Id ?? 0], cancellationToken);

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
            await LockAsync([existing.Id], cancellationToken);
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
