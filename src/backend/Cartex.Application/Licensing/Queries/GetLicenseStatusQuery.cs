using Cartex.Application.Common.Interfaces;

namespace Cartex.Application.Licensing.Queries;

public record GetLicenseStatusQuery : IRequest<LicenseStatus>;

public sealed class GetLicenseStatusQueryHandler(ILicenseService licenseService)
    : IRequestHandler<GetLicenseStatusQuery, LicenseStatus>
{
    public Task<LicenseStatus> Handle(GetLicenseStatusQuery request, CancellationToken cancellationToken) =>
        licenseService.GetStatusAsync(cancellationToken);
}
