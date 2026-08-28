using System.Linq.Expressions;
using Cartex.Application.Common.Finance;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Customers;

/// QARZ-24: mijozning boshlang'ich qoldig'i hali biznes tarixi bo'lmaganda tuzatiladi.
/// "Toza" degani - hisob-kitobga kiradigan amal yo'q: savdo, to'lov, qaytarish, kirim.
/// Tugallanmagan savat va navbat amal hisoblanmaydi - ular pul harakatini yaratmaydi.
public sealed class CustomerOpeningBalance(IApplicationDbContext db, ILedgerService ledger, ICurrencyService currency)
{
    public const string Description = "Boshlang'ich qoldiq";

    // Boshlang'ich yozuv hech qanday hujjatga bog'lanmaydi - uni tarixdagi har qanday
    // haqiqiy hodisadan ajratadigan yagona belgi shu.
    // Faqat qarz va avans hisoblari: bonus pul emas va uning yozuvi bu yerga tegishli emas
    // (`QARZ-21`) - uni o'chirish qoldiq bilan defterni bir-biridan ajratib yuborardi.
    private static readonly Expression<Func<Transaction, bool>> Opening =
        x => x.SaleId == null
            && x.CustomerPaymentDocumentId == null
            && x.CustomerReturnDocumentId == null
            && x.CustomerRefundDocumentId == null
            && x.PartnerRedemptionDocumentId == null
            && x.SupplyId == null
            && ((x.ToAccount != null && (x.ToAccount.Type == AccountType.Debt || x.ToAccount.Type == AccountType.CustomerAdvance))
                || (x.FromAccount != null && (x.FromAccount.Type == AccountType.Debt || x.FromAccount.Type == AccountType.CustomerAdvance)));

    private static readonly Expression<Func<Transaction, bool>> NotOpening =
        Expression.Lambda<Func<Transaction, bool>>(Expression.Not(Opening.Body), Opening.Parameters);

    public static async Task<bool> IsUntouchedAsync(
        IApplicationDbContext db, long customerId, CancellationToken cancellationToken) =>
        !await db.Sales.AnyAsync(x => x.CustomerId == customerId, cancellationToken)
        && !await CustomerTransactions(db, customerId).AnyAsync(NotOpening, cancellationToken);

    /// Tuzatish va o'chirish yo'li uchun: hisoblar avval qulflanadi, shunda parallel birinchi
    /// savdo "toza" tekshiruvi bilan qoldiqni tozalash orasiga tushib qolmaydi.
    public async Task<bool> IsUntouchedForWriteAsync(long customerId, CancellationToken cancellationToken)
    {
        await CustomerAccounts.LockAsync(db, ledger, customerId, cancellationToken);
        return await IsUntouchedAsync(db, customerId, cancellationToken);
    }

    /// Boshlang'ich yozuvni o'chiradi va tegishli hisoblarni nolga tushiradi. Toza mijozda
    /// bu yozuvlar yagona bo'lgani uchun qoldiq aynan nolga qaytadi. Oldingi qiymat auditga
    /// yoziladi: musbat - qarz, manfiy - haqdorlik.
    public async Task<decimal> ClearAsync(long customerId, CancellationToken cancellationToken)
    {
        var accounts = (await CustomerAccounts.LockAsync(db, ledger, customerId, cancellationToken))
            .Where(x => x.Type == AccountType.Debt || x.Type == AccountType.CustomerAdvance)
            .ToList();
        var previous = accounts.Sum(x => x.Type == AccountType.Debt ? x.Balance : -x.Balance);
        foreach (var account in accounts) account.Balance = 0;

        var opening = await CustomerTransactions(db, customerId).Where(Opening).ToListAsync(cancellationToken);
        db.Transactions.RemoveRange(opening);
        return previous;
    }

    /// Yangi boshlang'ich yozuvni yozadi. Musbat qiymat qarz, manfiy qiymat haqdorlik (`QARZ-23`).
    public async Task PostAsync(
        long customerId, decimal amount, string? currencyCode, long userId, CancellationToken cancellationToken)
    {
        if (amount == 0) return;
        var baseCode = await currency.BaseAsync(cancellationToken);
        var code = string.IsNullOrWhiteSpace(currencyCode) ? baseCode : currencyCode.Trim().ToUpperInvariant();
        await currency.EnsureSalesAllowedAsync(code, cancellationToken);

        var rate = code == baseCode ? 1m : await currency.RateAsync(code, cancellationToken);
        var absolute = Math.Abs(amount);
        var account = amount > 0
            ? await ledger.CustomerAccountAsync(customerId, AccountType.Debt, cancellationToken, code)
            : await ledger.CustomerAccountAsync(customerId, AccountType.CustomerAdvance, cancellationToken, code);
        var operation = amount > 0 ? OperationType.DebtCharge : OperationType.CustomerAdvance;
        (await ledger.PostAsync(operation, absolute, null, account, userId, cancellationToken, null, rate)).Description = Description;
    }

    /// Hozirgi boshlang'ich qoldiq: musbat - qarz, manfiy - haqdorlik (`QARZ-23`).
    public static async Task<(decimal Amount, string? Currency)> CurrentAsync(
        IApplicationDbContext db, long customerId, CancellationToken cancellationToken)
    {
        var rows = await CustomerTransactions(db, customerId).Where(Opening)
            .Select(x => new { x.OperationType, x.Amount, x.Currency })
            .ToListAsync(cancellationToken);
        var amount = rows.Sum(x => x.OperationType == OperationType.CustomerAdvance ? -x.Amount : x.Amount);
        return (amount, rows.Count > 0 ? rows[0].Currency : null);
    }

    private static IQueryable<Transaction> CustomerTransactions(IApplicationDbContext db, long customerId) =>
        db.Transactions.Where(x =>
            (x.FromAccount != null && x.FromAccount.CustomerId == customerId)
            || (x.ToAccount != null && x.ToAccount.CustomerId == customerId));
}
