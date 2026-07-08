using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;

namespace Cartex.Application.Settings.Queries;

public record ReminderSettingsDto(bool Enabled, int MinDaysOverdue, int RepeatEveryDays, decimal MinBalance, int SendHourLocal, List<string> Channels, string? OverdueTemplate = null, string? DueSoonTemplate = null);

public record GetReminderSettingsQuery : IRequest<ReminderSettingsDto>;

public sealed class GetReminderSettingsQueryHandler(ISettingsService settings)
    : IRequestHandler<GetReminderSettingsQuery, ReminderSettingsDto>
{
    public async Task<ReminderSettingsDto> Handle(GetReminderSettingsQuery request, CancellationToken cancellationToken)
    {
        var cfg = await settings.GetAsync<ReminderSettings>(SettingKeys.Reminder, cancellationToken) ?? new ReminderSettings();
        return new ReminderSettingsDto(cfg.Enabled, cfg.MinDaysOverdue, cfg.RepeatEveryDays, cfg.MinBalance, cfg.SendHourLocal,
            cfg.Channels.Select(c => c.ToString()).ToList(), cfg.OverdueTemplate, cfg.DueSoonTemplate);
    }
}
