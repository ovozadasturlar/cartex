using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Features.Commands;

/// The owner's switch applies only while the vendor license is active (SOZ-08a).
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

        if (request.IsEnabled && !feature.IsEnabled)
            throw new BusinessRuleException("Bu modul tarifingizda yo'q.", "module_not_licensed");

        feature.OwnerEnabled = request.IsEnabled;
        await db.SaveChangesAsync(cancellationToken);
        features.Invalidate();

        audit.Add("module", "features", feature.Id, new { feature.Code, request.IsEnabled });
        return Unit.Value;
    }
}
