namespace Cartex.Shared.Models.Loyalty;

public record LoyaltyProgramDto(bool IsEnabled, decimal TotalPercent, decimal CashbackRounding, List<CashbackRuleDto> Rules, string DiscountCombineMode = "Priority");

public record CashbackRuleDto(long Id, string Scope, long TargetId, string TargetName, string Method, decimal Value, int Priority, bool ExcludeFromTotalPercent);
