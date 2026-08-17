using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Shared.Models.Settings;

namespace Cartex.Application.Settings.Queries;

public record GetReceiptSettingsQuery : IRequest<ReceiptSettingsDto>;

public sealed class GetReceiptSettingsQueryHandler(ISettingsService settings)
    : IRequestHandler<GetReceiptSettingsQuery, ReceiptSettingsDto>
{
    public async Task<ReceiptSettingsDto> Handle(GetReceiptSettingsQuery request, CancellationToken cancellationToken)
    {
        var cfg = await settings.GetAsync<ReceiptSettings>(SettingKeys.Receipt, cancellationToken) ?? new ReceiptSettings();
        var notification = await settings.GetAsync<NotificationSettings>(SettingKeys.Notification, cancellationToken);
        return new ReceiptSettingsDto(
            cfg.HeaderText,
            cfg.FooterText,
            cfg.PaperWidth,
            cfg.PaperFormat,
            cfg.ShowBusinessName,
            cfg.ShowBranchName,
            cfg.ShowAddress,
            cfg.ShowPhone,
            cfg.ShowCashier,
            cfg.ShowCustomer,
            cfg.ShowReceiptNumber,
            cfg.ShowPaymentDetails,
            cfg.ShowQrCode,
            cfg.ShowElectronicLink,
            notification?.PublicBaseUrl,
            cfg.ShowLogo,
            cfg.ShowCustomerPhone,
            cfg.ShowCustomerEmail);
    }
}
