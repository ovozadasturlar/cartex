namespace Cartex.Shared.Models.Business;

public record BusinessDto(string Name, string? LegalName, string Currency, bool IsOnboarded, string? Phone = null, string? Address = null, string? LogoImageKey = null, bool Multicurrency = false);

public record UpdateBusinessRequest(string Name, string? LegalName, string Currency, string? Phone = null, string? Address = null, string? LogoImageKey = null);
