namespace Cartex.Shared.Models.Loyalty;

public record UpdateLoyaltyProgramRequest(bool IsEnabled, decimal TotalPercent, decimal CashbackRounding = 0, string DiscountCombineMode = "Priority");
