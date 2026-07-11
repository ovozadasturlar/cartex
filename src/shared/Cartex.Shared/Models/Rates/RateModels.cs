namespace Cartex.Shared.Models.Rates;

public record RateDto(string Code, decimal Rate, DateTime EffectiveAt, string Source);

public record SetRateRequest(string Code, decimal Rate);

public record CurrencyDto(string Code, string Name, bool IsSystem, bool IsEnabled, bool IsDefault, bool IsBase, decimal? Rate, DateTime? RateAt);

public record CreateCurrencyRequest(string Code, string Name);

public record UpdateCurrencyRequest(string Code, bool IsEnabled, bool IsDefault);
