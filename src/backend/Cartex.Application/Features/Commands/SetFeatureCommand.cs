using Cartex.Application.Common.Messaging;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Features.Commands;

public record SetFeatureCommand(string Code, bool IsEnabled) : ICommand<Unit>;

public sealed class SetFeatureCommandHandler(IApplicationDbContext db, IFeatureStateProvider features)
    : IRequestHandler<SetFeatureCommand, Unit>
{
    public async Task<Unit> Handle(SetFeatureCommand request, CancellationToken cancellationToken)
    {
        var feature = await db.Features.FirstOrDefaultAsync(f => f.Code == request.Code, cancellationToken)
            ?? throw new NotFoundException("Feature not found.");

        feature.IsEnabled = request.IsEnabled;

        if (request.IsEnabled && request.Code is FeatureCatalog.PricingMulticurrency or FeatureCatalog.SalesMulticurrency)
        {
            var infrastructure = await db.Features.FirstOrDefaultAsync(f => f.Code == FeatureCatalog.Multicurrency, cancellationToken);
            if (infrastructure is not null)
                infrastructure.IsEnabled = true;
        }

        if (!request.IsEnabled && request.Code == FeatureCatalog.Multicurrency)
        {
            var children = await db.Features
                .Where(f => f.Code == FeatureCatalog.PricingMulticurrency || f.Code == FeatureCatalog.SalesMulticurrency)
                .ToListAsync(cancellationToken);
            foreach (var child in children)
                child.IsEnabled = false;
        }

        await db.SaveChangesAsync(cancellationToken);
        features.Invalidate();
        return Unit.Value;
    }
}
