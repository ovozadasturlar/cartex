using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Onboarding;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Common;
using Cartex.Persistence;
using ProductType = Cartex.Domain.Entities.ProductType;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Business.Commands;

public record CompleteOnboardingCommand(string? Preset = null, string? Language = null) : ICommand<Unit>;

public sealed class CompleteOnboardingCommandHandler(ISettingsService settings, IApplicationDbContext db, IFeatureStateProvider features, IAuditService audit)
    : IRequestHandler<CompleteOnboardingCommand, Unit>
{
    public async Task<Unit> Handle(CompleteOnboardingCommand request, CancellationToken cancellationToken)
    {
        if (await settings.GetAsync<bool>(SettingKeys.Onboarded, cancellationToken))
            return Unit.Value;

        if (OnboardingPresets.Find(request.Preset) is { } preset)
        {
            foreach (var code in preset.EnableFeatures)
            {
                var feature = await db.Features.FirstOrDefaultAsync(f => f.Code == code, cancellationToken);
                if (feature is not null) feature.IsEnabled = true;
            }

            var names = preset.ProductTypes.Select(t => t.Name(request.Language)).ToList();
            var existing = await db.ProductTypes.Where(t => names.Contains(t.Name)).Select(t => t.Name).ToListAsync(cancellationToken);
            foreach (var type in preset.ProductTypes.Where(t => !existing.Contains(t.Name(request.Language))))
                db.ProductTypes.Add(new ProductType { Name = type.Name(request.Language), TracksExpiry = type.TracksExpiry });

            await db.SaveChangesAsync(cancellationToken);
            features.Invalidate();
        }

        audit.Add("onboarding", "business", null, new { preset = request.Preset });
        await settings.SetAsync(SettingKeys.Onboarded, true, cancellationToken);
        return Unit.Value;
    }
}
