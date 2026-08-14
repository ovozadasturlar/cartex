using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Persistence;
using FluentValidation;

namespace Cartex.Application.Settings.Commands;

public record UpdateProformaSettingsCommand(
    string? HeaderText,
    string? FooterText,
    int PaperWidth,
    string PaperFormat = "Thermal",
    bool ShowBusinessName = true,
    bool ShowAddress = true,
    bool ShowPhone = true,
    bool ShowSeller = true,
    bool ShowCustomer = true,
    bool ShowNote = true,
    bool ShowCartCode = true) : ICommand<Unit>;

public sealed class UpdateProformaSettingsCommandHandler(ISettingsService settings, IAuditService audit)
    : IRequestHandler<UpdateProformaSettingsCommand, Unit>
{
    public async Task<Unit> Handle(UpdateProformaSettingsCommand request, CancellationToken cancellationToken)
    {
        var cfg = new ProformaSettings
        {
            HeaderText = string.IsNullOrWhiteSpace(request.HeaderText) ? null : request.HeaderText.Trim(),
            FooterText = string.IsNullOrWhiteSpace(request.FooterText) ? null : request.FooterText.Trim(),
            PaperWidth = request.PaperWidth,
            PaperFormat = request.PaperFormat,
            ShowBusinessName = request.ShowBusinessName,
            ShowAddress = request.ShowAddress,
            ShowPhone = request.ShowPhone,
            ShowSeller = request.ShowSeller,
            ShowCustomer = request.ShowCustomer,
            ShowNote = request.ShowNote,
            ShowCartCode = request.ShowCartCode
        };
        audit.Add("settings", "settings", null, new { section = "proforma" });
        await settings.SetAsync(SettingKeys.Proforma, cfg, cancellationToken);
        return Unit.Value;
    }
}

public sealed class UpdateProformaSettingsCommandValidator : AbstractValidator<UpdateProformaSettingsCommand>
{
    public UpdateProformaSettingsCommandValidator()
    {
        RuleFor(x => x.PaperWidth).Must(w => w is 32 or 42 or 48);
        RuleFor(x => x.PaperFormat).Must(f => f is "Thermal" or "A5" or "A4");
        RuleFor(x => x.HeaderText).MaximumLength(200);
        RuleFor(x => x.FooterText).MaximumLength(200);
    }
}
