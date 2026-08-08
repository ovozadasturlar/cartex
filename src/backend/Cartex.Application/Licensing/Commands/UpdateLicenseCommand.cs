using Cartex.Application.Common.Interfaces;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using LicenseState = Cartex.Domain.Entities.LicenseState;

namespace Cartex.Application.Licensing.Commands;

public record UpdateLicenseCommand(string Tariff, DateTime? ExpiresAt, string? EnabledFeatures) : ICommand<Unit>;

public sealed class UpdateLicenseCommandHandler(IApplicationDbContext db, ILicenseService license) : IRequestHandler<UpdateLicenseCommand, Unit>
{
    public async Task<Unit> Handle(UpdateLicenseCommand request, CancellationToken cancellationToken)
    {
        var state = await db.LicenseStates.FirstOrDefaultAsync(cancellationToken);
        if (state is null)
        {
            state = new LicenseState();
            db.LicenseStates.Add(state);
        }

        state.Tariff = request.Tariff;
        state.ExpiresAt = request.ExpiresAt is { } e ? DateTime.SpecifyKind(e, DateTimeKind.Utc) : null;
        state.EnabledFeatures = request.EnabledFeatures;
        await db.SaveChangesAsync(cancellationToken);
        license.Invalidate();
        return Unit.Value;
    }
}
