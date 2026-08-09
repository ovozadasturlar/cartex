namespace Cartex.Shared.Models.Business;

public record BusinessDto(
    string Name,
    string? LegalName,
    string Currency,
    bool IsOnboarded,
    string? Phone = null,
    string? Address = null,
    string? LogoImageKey = null,
    bool Multicurrency = false,
    string? Telegram = null,
    string? Website = null,
    bool PricingMulticurrency = false,
    bool SalesMulticurrency = false,
    string? MonochromeLogoImageKey = null);

public record UpdateBusinessRequest(string Name, string? LegalName, string Currency, string? Phone = null, string? Address = null, string? LogoImageKey = null, string? Telegram = null, string? Website = null, string? MonochromeLogoImageKey = null);

public record CompleteOnboardingRequest(string? Preset = null, string? Language = null);
