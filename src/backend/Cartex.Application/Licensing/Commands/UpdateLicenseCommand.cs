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
        return Unit.Value;
    }
}
