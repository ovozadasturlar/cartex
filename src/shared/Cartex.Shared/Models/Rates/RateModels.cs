namespace Cartex.Shared.Models.Rates;

public record RateDto(string Code, decimal Rate, DateTime EffectiveAt, string Source);

public record SetRateRequest(string Code, decimal Rate);

public record CurrencyDto(string Code, string Name, bool IsSystem, bool IsEnabled, bool IsDefault, bool IsBase, decimal? Rate, DateTime? RateAt, string Symbol = "", string SymbolPosition = "Suffix", int DecimalDigits = 2);

public record CreateCurrencyRequest(string Code, string Name, string Symbol = "", string SymbolPosition = "Suffix", int DecimalDigits = 2);

public record UpdateCurrencyRequest(string Code, bool IsEnabled, bool IsDefault, string? Symbol = null, string? SymbolPosition = null, int? DecimalDigits = null);
