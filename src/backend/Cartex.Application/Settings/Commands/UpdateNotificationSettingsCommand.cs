using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Enums;
using Cartex.Application.Common.Messaging;

namespace Cartex.Application.Settings.Commands;

public record UpdateNotificationSettingsCommand(List<NotificationChannel> Channels, bool CopyToAdmin, string? PublicBaseUrl, string? TelegramFormat = null, string? EmailFormat = null) : ICommand<Unit>;

public sealed class UpdateNotificationSettingsCommandHandler(ISettingsService settings)
    : IRequestHandler<UpdateNotificationSettingsCommand, Unit>
{
    public async Task<Unit> Handle(UpdateNotificationSettingsCommand request, CancellationToken cancellationToken)
    {
        var cfg = await settings.GetAsync<NotificationSettings>(SettingKeys.Notification, cancellationToken) ?? new();
        cfg.Channels = request.Channels ?? [];
        cfg.CopyToAdmin = request.CopyToAdmin;
        cfg.PublicBaseUrl = request.PublicBaseUrl;
        if (Enum.TryParse<ReceiptDeliveryFormat>(request.TelegramFormat, true, out var tf)) cfg.TelegramFormat = tf;
        if (Enum.TryParse<ReceiptDeliveryFormat>(request.EmailFormat, true, out var ef)) cfg.EmailFormat = ef;

        await settings.SetAsync(SettingKeys.Notification, cfg, cancellationToken);
        return Unit.Value;
    }
}
