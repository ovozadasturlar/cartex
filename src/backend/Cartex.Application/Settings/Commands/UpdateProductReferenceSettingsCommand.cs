using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Persistence;
using Cartex.Shared.Models.Products;
using FluentValidation;

namespace Cartex.Application.Settings.Commands;

public sealed record UpdateProductReferenceSettingsCommand(ProductReferenceSettingsDto Settings) : ICommand<Unit>;

public sealed class UpdateProductReferenceSettingsCommandHandler(ISettingsService settings, IAuditService audit)
    : IRequestHandler<UpdateProductReferenceSettingsCommand, Unit>
{
    public async Task<Unit> Handle(UpdateProductReferenceSettingsCommand request, CancellationToken cancellationToken)
    {
        var current = await settings.GetAsync<ProductReferenceSettings>(SettingKeys.ProductReference, cancellationToken)
            ?? new ProductReferenceSettings();
        current.IsEnabled = request.Settings.IsEnabled;
        current.SourceType = request.Settings.SourceType;
        current.SpreadsheetId = request.Settings.SpreadsheetId?.Trim() ?? string.Empty;
        current.SheetName = request.Settings.SheetName?.Trim() ?? string.Empty;
        current.BarcodeColumn = request.Settings.BarcodeColumn?.Trim() ?? string.Empty;
        current.NameColumn = request.Settings.NameColumn?.Trim() ?? string.Empty;
        current.UnitColumn = request.Settings.UnitColumn?.Trim() ?? string.Empty;
        current.CategoryColumn = request.Settings.CategoryColumn?.Trim() ?? string.Empty;
        current.ManufacturerColumn = request.Settings.ManufacturerColumn?.Trim() ?? string.Empty;
        current.PackQtyColumn = request.Settings.PackQtyColumn?.Trim() ?? string.Empty;
        current.PriceColumn = request.Settings.PriceColumn?.Trim() ?? string.Empty;
        current.AutoFillPrice = request.Settings.AutoFillPrice;
        current.SyncSchedule = request.Settings.SyncSchedule;
        await settings.SetAsync(SettingKeys.ProductReference, current, cancellationToken);
        audit.Add("settings", "settings", null, new { section = "productReference" });
        return Unit.Value;
    }
}

public sealed class UpdateProductReferenceSettingsCommandValidator : AbstractValidator<UpdateProductReferenceSettingsCommand>
{
    public UpdateProductReferenceSettingsCommandValidator()
    {
        RuleFor(x => x.Settings.SourceType).Equal("GoogleSheets");
        RuleFor(x => x.Settings.SyncSchedule).Must(x => x is "Manual" or "Daily");
        RuleFor(x => x.Settings.SpreadsheetId).NotEmpty().MaximumLength(200).When(x => x.Settings.IsEnabled);
        RuleFor(x => x.Settings.SheetName).NotEmpty().MaximumLength(200).When(x => x.Settings.IsEnabled);
        RuleFor(x => x.Settings.BarcodeColumn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Settings.NameColumn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Settings.UnitColumn).MaximumLength(100);
        RuleFor(x => x.Settings.CategoryColumn).MaximumLength(100);
        RuleFor(x => x.Settings.ManufacturerColumn).MaximumLength(100);
        RuleFor(x => x.Settings.PackQtyColumn).MaximumLength(100);
        RuleFor(x => x.Settings.PriceColumn).MaximumLength(100);
    }
}
