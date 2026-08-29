using Cartex.Domain.Common.Exceptions;

namespace Cartex.Application.Common.Settings;

/// QARZ-22: kredit limitidan oshishni rad etish yoki ogohlantirish bilan o'tkazish qarori.
/// Savdodagi qarz ham, naqd qarz ham shu yerdan o'tadi — limit mijozning umumiy majburiyati
/// haqidagi savol, u qaysi kanal orqali yuzaga kelgani haqidagi emas.
public static class CreditLimits
{
    public const string Warning = "credit_limit_exceeded";

    /// Ogohlantirish bilan o'tkazilsa `true` qaytadi, aks holda rad etadi. Oflayn replay
    /// `OFF-22` bo'yicha hech qachon rad etmaydi — nol limitda ham: tovar allaqachon berilgan,
    /// rad etish ma'lumotni yo'qotadi. Onlayn yo'lda `0` limit (`SOZ-02a`) chegara emas, taqiq —
    /// u `Warn` rejimida ham ochilmaydi.
    public static bool WarnOrThrow(decimal creditLimit, SalesPolicySettings policy, string message, bool fromOfflineReplay = false)
    {
        if (fromOfflineReplay || (creditLimit > 0 && policy.CreditLimitEnforcement == "Warn"))
            return true;
        throw new BusinessRuleException(message, Warning);
    }
}
