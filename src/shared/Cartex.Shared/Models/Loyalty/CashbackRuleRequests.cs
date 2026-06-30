namespace Cartex.Shared.Models.Loyalty;

public record CreateCashbackRuleRequest(string Scope, long TargetId, string Method, decimal Value, int Priority, bool ExcludeFromTotalPercent = false);

public record UpdateCashbackRuleRequest(string Scope, long TargetId, string Method, decimal Value, int Priority, bool ExcludeFromTotalPercent = false);
