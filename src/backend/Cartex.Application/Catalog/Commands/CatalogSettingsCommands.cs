using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Persistence;
using Cartex.Shared.Models.Catalog;
using FluentValidation;

namespace Cartex.Application.Catalog.Commands;

public sealed record UpdateCatalogSettingsCommand(CatalogSettingsDto Settings) : ICommand<Unit>;

public sealed class UpdateCatalogSettingsCommandHandler(ISettingsService settings, IAuditService audit)
    : IRequestHandler<UpdateCatalogSettingsCommand, Unit>
{
    public async Task<Unit> Handle(UpdateCatalogSettingsCommand request, CancellationToken cancellationToken)
    {
        var current = await settings.GetAsync<ProductReferenceSettings>(SettingKeys.ProductReference, cancellationToken)
            ?? new ProductReferenceSettings();
        current.Mode = request.Settings.Mode;
        current.EndpointBaseUrl = request.Settings.EndpointBaseUrl.Trim();
        current.ImageBaseUrl = request.Settings.ImageBaseUrl.Trim();
        await settings.SetAsync(SettingKeys.ProductReference, current, cancellationToken);
        audit.Add("settings", "settings", null, new { section = "catalog", mode = current.Mode.ToString() });
        return Unit.Value;
    }
}

public sealed class UpdateCatalogSettingsCommandValidator : AbstractValidator<UpdateCatalogSettingsCommand>
{
    public UpdateCatalogSettingsCommandValidator()
    {
        RuleFor(x => x.Settings.Mode).IsInEnum();
        RuleFor(x => x.Settings.EndpointBaseUrl).NotEmpty().Must(BeAbsoluteHttps)
            .When(x => x.Settings.Mode == CatalogSourceMode.Online);
        RuleFor(x => x.Settings.EndpointBaseUrl).MaximumLength(500);
        RuleFor(x => x.Settings.ImageBaseUrl).MaximumLength(500);
        RuleFor(x => x.Settings.ImageBaseUrl).Must(BeAbsoluteHttps).When(x => x.Settings.ImageBaseUrl.Length > 0);
    }

    private static bool BeAbsoluteHttps(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps;
}

public sealed record UploadCatalogPackCommand(Stream Pack, Stream Manifest) : ICommand<CatalogPackDto>;

public sealed class UploadCatalogPackCommandHandler(ICatalogPackStore packs, IAuditService audit)
    : IRequestHandler<UploadCatalogPackCommand, CatalogPackDto>
{
    public async Task<CatalogPackDto> Handle(UploadCatalogPackCommand request, CancellationToken cancellationToken)
    {
        var pack = await packs.SaveAsync(request.Pack, request.Manifest, cancellationToken);
        audit.Add("catalogPackUploaded", "settings", null,
            new { pack.ShopType, pack.Version, pack.RowCount });
        return pack;
    }
}

public sealed record DeleteCatalogPackCommand : ICommand<Unit>;

public sealed class DeleteCatalogPackCommandHandler(IApplicationDbContext db, ICatalogPackStore packs, IAuditService audit)
    : IRequestHandler<DeleteCatalogPackCommand, Unit>
{
    public async Task<Unit> Handle(DeleteCatalogPackCommand request, CancellationToken cancellationToken)
    {
        if (await packs.StateAsync(cancellationToken) is { Pack: not null })
        {
            audit.Add("catalogPackDeleted", "settings", null);
            await db.RunAfterCommitAsync(() => packs.DeleteAsync(cancellationToken));
        }

        return Unit.Value;
    }
}
