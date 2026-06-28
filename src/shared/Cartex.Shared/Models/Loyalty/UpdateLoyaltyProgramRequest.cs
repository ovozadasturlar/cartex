namespace Cartex.Shared.Models.Loyalty;

public record UpdateLoyaltyProgramRequest(bool IsEnabled, string Base, decimal TotalPercent);
