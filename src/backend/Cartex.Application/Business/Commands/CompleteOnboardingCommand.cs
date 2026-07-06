using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;

namespace Cartex.Application.Business.Commands;

public record CompleteOnboardingCommand : ICommand<Unit>;

public sealed class CompleteOnboardingCommandHandler(ISettingsService settings)
    : IRequestHandler<CompleteOnboardingCommand, Unit>
{
    public async Task<Unit> Handle(CompleteOnboardingCommand request, CancellationToken cancellationToken)
    {
        await settings.SetAsync(SettingKeys.Onboarded, true, cancellationToken);
        return Unit.Value;
    }
}
