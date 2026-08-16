using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Features.Commands;

/// The owner's switch, kept apart from the vendor's. A shop turning a module off must not look
/// like the tariff lost it, and a tariff change must not undo what the owner chose (SOZ-08).
public sealed record SetOwnerModuleCommand(string Code, bool IsEnabled) : ICommand<Unit>;

public sealed class SetOwnerModuleCommandHandler(
    IApplicationDbContext db,
    IFeatureStateProvider features,
    IAuditService audit) : IRequestHandler<SetOwnerModuleCommand, Unit>
{
    public async Task<Unit> Handle(SetOwnerModuleCommand request, CancellationToken cancellationToken)
    {
        if (!FeatureCatalog.ConfigurableCodes.Contains(request.Code))
            throw new BusinessRuleException("Bu modulni o'chirib bo'lmaydi.", "module_not_configurable");

        var feature = await db.Features.FirstOrDefaultAsync(f => f.Code == request.Code, cancellationToken)
            ?? throw new NotFoundException("Feature not found.");

        feature.OwnerEnabled = request.IsEnabled;
        await db.SaveChangesAsync(cancellationToken);
        features.Invalidate();

        audit.Add("module", "features", feature.Id, new { feature.Code, request.IsEnabled });
        return Unit.Value;
    }
}
