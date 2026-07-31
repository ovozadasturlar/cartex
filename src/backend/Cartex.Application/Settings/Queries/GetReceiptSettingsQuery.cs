using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;

namespace Cartex.Application.Settings.Queries;

public record ReceiptSettingsDto(
    string? HeaderText,
    string? FooterText,
    int PaperWidth,
    string PaperFormat = "Thermal",
    bool ShowBusinessName = true,
    bool ShowBranchName = true,
    bool ShowAddress = true,
    bool ShowPhone = true,
    bool ShowCashier = true,
    bool ShowCustomer = true,
    bool ShowReceiptNumber = true,
    bool ShowPaymentDetails = true,
    bool ShowQrCode = true,
    bool ShowElectronicLink = true,
    string? PublicReceiptBaseUrl = null);

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
            notification?.PublicBaseUrl);
    }
}
