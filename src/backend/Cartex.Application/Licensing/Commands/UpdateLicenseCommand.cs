using Cartex.Application.Common.Interfaces;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Licensing.Commands;

public record UpdateLicenseCommand(string Tariff, DateTime? ExpiresAt, string? EnabledFeatures) : ICommand<Unit>;

public sealed class UpdateLicenseCommandHandler(IApplicationDbContext db, ILicenseService license) : IRequestHandler<UpdateLicenseCommand, Unit>
{
    public async Task<Unit> Handle(UpdateLicenseCommand request, CancellationToken cancellationToken)
    {
        var state = await db.LicenseStates.SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(
                "Litsenziya qatori topilmadi — u seed paytida yaratiladi va qayta yaratilmaydi.",
                "license_state_missing");

        state.Tariff = request.Tariff;
        state.ExpiresAt = request.ExpiresAt is { } e ? DateTime.SpecifyKind(e, DateTimeKind.Utc) : null;
        state.EnabledFeatures = request.EnabledFeatures;
        await db.SaveChangesAsync(cancellationToken);
        license.Invalidate();

        // SOZ-08b: features.is_enabled aynan joriy tarifni aks ettiradi, litsenziya har o'zgarganda
        // qayta hisoblanadi. SOZ-08a: tarifdan chiqqan modulning egа kaliti ham o'chiriladi.
        var licensed = await license.GetTariffFeaturesAsync(cancellationToken);
        foreach (var feature in await db.Features.ToListAsync(cancellationToken))
        {
            var isLicensed = licensed.Contains(feature.Code);
            if (!isLicensed && feature.IsEnabled)
                feature.OwnerEnabled = false;
            feature.IsEnabled = isLicensed;
        }
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
