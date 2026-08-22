using Cartex.Domain.Common.Exceptions;

namespace Cartex.Application.Common.Settings;

/// QARZ-22: kredit limitidan oshishni rad etish yoki ogohlantirish bilan o'tkazish qarori.
/// Savdodagi qarz ham, naqd qarz ham shu yerdan o'tadi — limit mijozning umumiy majburiyati
/// haqidagi savol, u qaysi kanal orqali yuzaga kelgani haqidagi emas.
public static class CreditLimits
{
    public const string Warning = "credit_limit_exceeded";

    /// Ogohlantirish bilan o'tkazilsa `true` qaytadi, aks holda rad etadi. `0` limit (`SOZ-02a`)
    /// chegara emas, taqiq — u hech qanday rejimda ochilmaydi. Oflayn replay `OFF-22` bo'yicha
    /// hech qachon rad etmaydi: tovar allaqachon berilgan, rad etish ma'lumotni yo'qotadi.
    public static bool WarnOrThrow(decimal creditLimit, SalesPolicySettings policy, string message, bool fromOfflineReplay = false)
    {
        if (creditLimit > 0 && (fromOfflineReplay || policy.CreditLimitEnforcement == "Warn"))
            return true;
        throw new BusinessRuleException(message, Warning);
    }
}
