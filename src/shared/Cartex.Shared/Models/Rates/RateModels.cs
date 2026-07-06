namespace Cartex.Shared.Models.Rates;

public record RateDto(string Code, decimal Rate, DateTime EffectiveAt, string Source);

public record SetRateRequest(string Code, decimal Rate);
