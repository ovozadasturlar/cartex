using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Persistence;
using FluentValidation;

namespace Cartex.Application.Settings.Commands;

public record UpdateBarcodeLabelSettingsCommand(
    bool DefaultWithPrice,
    bool AllowPriceOverride,
    bool ShowSku,
    int NameLines,
    string CurrencyDisplay,
    string CurrencyCase,
    string PriceCurrencyMode) : ICommand<Unit>;

public sealed class UpdateBarcodeLabelSettingsCommandHandler(ISettingsService settings, IAuditService audit)
    : IRequestHandler<UpdateBarcodeLabelSettingsCommand, Unit>
{
    public async Task<Unit> Handle(UpdateBarcodeLabelSettingsCommand request, CancellationToken cancellationToken)
    {
        var value = new BarcodeLabelSettings
        {
            DefaultWithPrice = request.DefaultWithPrice,
            AllowPriceOverride = request.AllowPriceOverride,
            ShowSku = request.ShowSku,
            NameLines = request.NameLines,
            CurrencyDisplay = request.CurrencyDisplay,
            CurrencyCase = request.CurrencyCase,
            PriceCurrencyMode = request.PriceCurrencyMode
        };
        audit.Add("settings", "settings", null, new { section = "barcodeLabel" });
        await settings.SetAsync(SettingKeys.BarcodeLabel, value, cancellationToken);
        return Unit.Value;
    }
}

public sealed class UpdateBarcodeLabelSettingsCommandValidator : AbstractValidator<UpdateBarcodeLabelSettingsCommand>
{
    public UpdateBarcodeLabelSettingsCommandValidator()
    {
        RuleFor(x => x.NameLines).InclusiveBetween(0, 2);
        RuleFor(x => x.CurrencyDisplay).Must(x => x is "symbol" or "code");
        RuleFor(x => x.CurrencyCase).Must(x => x is "original" or "upper" or "lower");
        RuleFor(x => x.PriceCurrencyMode).Must(x => x is "product" or "default");
    }
}
