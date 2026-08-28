using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;

namespace Cartex.Application.Common.Finance;

/// SMENA-08: naqd chiqim daftardagi kassa qoldig'i bilan cheklanadi. Smenaning boshlang'ich
/// qoldig'i daftarga yozilmaydi, shuning uchun yashikda jismonan pul turgan bo'lsa ham undan
/// to'lab bo'lmaydi — aks holda o'sha pul smena yopilishida kamomad bo'lib chiqardi.
public static class CashPayout
{
    public static async Task<Account> AccountAsync(
        ILedgerService ledger, long branchId, decimal amount, CancellationToken cancellationToken, string? currencyCode = null)
    {
        var cash = await ledger.BranchAccountAsync(branchId, AccountType.Cash, cancellationToken, currencyCode);
        if (cash.Balance < amount)
            throw new BusinessRuleException("Kassada yetarli naqd mablag' yo'q.", "cash_balance_insufficient");
        return cash;
    }
}
