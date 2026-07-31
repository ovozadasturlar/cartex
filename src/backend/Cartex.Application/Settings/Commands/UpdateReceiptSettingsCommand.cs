using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Persistence;
using FluentValidation;

namespace Cartex.Application.Settings.Commands;

public record UpdateReceiptSettingsCommand(
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
    bool ShowElectronicLink = true) : ICommand<Unit>;

public sealed class UpdateReceiptSettingsCommandHandler(ISettingsService settings, IAuditService audit)
    : IRequestHandler<UpdateReceiptSettingsCommand, Unit>
{
    public async Task<Unit> Handle(UpdateReceiptSettingsCommand request, CancellationToken cancellationToken)
    {
        var cfg = new ReceiptSettings
        {
            HeaderText = string.IsNullOrWhiteSpace(request.HeaderText) ? null : request.HeaderText.Trim(),
            FooterText = string.IsNullOrWhiteSpace(request.FooterText) ? null : request.FooterText.Trim(),
            PaperWidth = request.PaperWidth,
            PaperFormat = request.PaperFormat,
            ShowBusinessName = request.ShowBusinessName,
            ShowBranchName = request.ShowBranchName,
            ShowAddress = request.ShowAddress,
            ShowPhone = request.ShowPhone,
            ShowCashier = request.ShowCashier,
            ShowCustomer = request.ShowCustomer,
            ShowReceiptNumber = request.ShowReceiptNumber,
            ShowPaymentDetails = request.ShowPaymentDetails,
            ShowQrCode = request.ShowQrCode,
            ShowElectronicLink = request.ShowElectronicLink
        };
        audit.Add("settings", "settings", null, new { section = "receipt" });
        await settings.SetAsync(SettingKeys.Receipt, cfg, cancellationToken);
        return Unit.Value;
    }
}

public sealed class UpdateReceiptSettingsCommandValidator : AbstractValidator<UpdateReceiptSettingsCommand>
{
    public UpdateReceiptSettingsCommandValidator()
    {
        RuleFor(x => x.PaperWidth).Must(w => w is 32 or 42 or 48);
        RuleFor(x => x.PaperFormat).Must(f => f is "Thermal" or "A5" or "A4");
        RuleFor(x => x.HeaderText).MaximumLength(200);
        RuleFor(x => x.FooterText).MaximumLength(200);
    }
}
