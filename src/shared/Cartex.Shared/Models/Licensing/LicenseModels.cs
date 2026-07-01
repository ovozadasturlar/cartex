namespace Cartex.Shared.Models.Licensing;

public record LicenseStatusDto(bool IsActive, string Tariff, DateTime? ExpiresAt, List<string> EnabledFeatures);

public record UpdateLicenseRequest(string Tariff, DateTime? ExpiresAt, string? EnabledFeatures);

public record LicenseOptionsDto(List<string> Tariffs, List<LicenseFeatureDto> Features);

public record LicenseFeatureDto(string Code, string Name, List<string> IncludedTariffs, List<string> Permissions);
