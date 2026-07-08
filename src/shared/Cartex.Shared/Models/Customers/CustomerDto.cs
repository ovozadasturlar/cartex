using Cartex.Shared.Models.Common;

namespace Cartex.Shared.Models.Customers;

public record CustomerDto(long Id, string FullName, string? LastName, string? Address, string? Phone, string? Email, string? CardBarcode, decimal DiscountPct, decimal CashbackBalance, decimal DebtBalance, decimal CreditLimit, bool NotificationsOptOut = false, bool HasTelegram = false, string? PreferredLanguage = null)
{
    public IReadOnlyList<CurrencyAmountDto> DebtBalances { get; init; } = [];
}
