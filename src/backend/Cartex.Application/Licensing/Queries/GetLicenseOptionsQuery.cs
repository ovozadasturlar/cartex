using Cartex.Domain.Authorization;

namespace Cartex.Application.Licensing.Queries;

public record LicenseFeatureOption(string Code, string Name, List<string> IncludedTariffs, List<string> Permissions);

public record LicenseOptions(List<string> Tariffs, List<LicenseFeatureOption> Features);

public record GetLicenseOptionsQuery : IRequest<LicenseOptions>;

public sealed class GetLicenseOptionsQueryHandler : IRequestHandler<GetLicenseOptionsQuery, LicenseOptions>
{
    public Task<LicenseOptions> Handle(GetLicenseOptionsQuery request, CancellationToken cancellationToken)
    {
        var tariffs = TariffCatalog.Map.Keys.ToList();
        var features = FeatureCatalog.ConfigurableCodes.Select(code => new LicenseFeatureOption(
            code,
            FeatureCatalog.Names[code],
            tariffs.Where(t => TariffCatalog.Map[t].Contains(code)).ToList(),
            FeatureCatalog.Map.TryGetValue(code, out var perms) ? perms.ToList() : [])).ToList();

        return Task.FromResult(new LicenseOptions(tariffs, features));
    }
}
