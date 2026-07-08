using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using FluentValidation;

namespace Cartex.Application.Settings.Commands;

public record UpdateReminderSettingsCommand(bool Enabled, int MinDaysOverdue, int RepeatEveryDays, decimal MinBalance, int SendHourLocal, List<string> Channels, string? OverdueTemplate = null, string? DueSoonTemplate = null) : ICommand<Unit>;

public sealed class UpdateReminderSettingsCommandHandler(ISettingsService settings)
    : IRequestHandler<UpdateReminderSettingsCommand, Unit>
{
    public async Task<Unit> Handle(UpdateReminderSettingsCommand request, CancellationToken cancellationToken)
    {
        var cfg = new ReminderSettings
        {
            Enabled = request.Enabled,
            MinDaysOverdue = request.MinDaysOverdue,
            RepeatEveryDays = request.RepeatEveryDays,
            MinBalance = request.MinBalance,
            SendHourLocal = request.SendHourLocal,
            OverdueTemplate = string.IsNullOrWhiteSpace(request.OverdueTemplate) ? null : request.OverdueTemplate.Trim(),
            DueSoonTemplate = string.IsNullOrWhiteSpace(request.DueSoonTemplate) ? null : request.DueSoonTemplate.Trim(),
            Channels = request.Channels
                .Select(c => Enum.TryParse<NotificationChannel>(c, true, out var parsed) ? parsed : (NotificationChannel?)null)
                .Where(c => c is not null)
                .Select(c => c!.Value)
                .Distinct()
                .ToList()
        };

        await settings.SetAsync(SettingKeys.Reminder, cfg, cancellationToken);
        return Unit.Value;
    }
}

public sealed class UpdateReminderSettingsCommandValidator : AbstractValidator<UpdateReminderSettingsCommand>
{
    public UpdateReminderSettingsCommandValidator()
    {
        RuleFor(x => x.MinDaysOverdue).InclusiveBetween(0, 365);
        RuleFor(x => x.RepeatEveryDays).InclusiveBetween(1, 365);
        RuleFor(x => x.MinBalance).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SendHourLocal).InclusiveBetween(0, 23);
        RuleFor(x => x.OverdueTemplate).MaximumLength(500);
        RuleFor(x => x.DueSoonTemplate).MaximumLength(500);
    }
}
