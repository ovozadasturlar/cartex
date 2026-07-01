namespace Cartex.Application.Common.Interfaces;

public sealed record LicenseStatus(bool IsActive, string Tariff, DateTime? ExpiresAt, List<string> EnabledFeatures);

public interface ILicenseService
{
    Task<LicenseStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<bool> IsActiveAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlySet<string>> GetTariffFeaturesAsync(CancellationToken cancellationToken = default);
    void Invalidate();
}
