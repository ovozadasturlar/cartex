using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Shared.Models.Products;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.ProductReference.Commands;

public sealed record SyncProductReferenceCommand(bool Scheduled = false) : ICommand<ProductReferenceSyncResultDto>;

public sealed class SyncProductReferenceCommandHandler(
    IApplicationDbContext db,
    ISettingsService settings,
    IProductReferenceSource source)
    : IRequestHandler<SyncProductReferenceCommand, ProductReferenceSyncResultDto>
{
    public async Task<ProductReferenceSyncResultDto> Handle(SyncProductReferenceCommand request, CancellationToken cancellationToken)
    {
        var config = await settings.GetAsync<ProductReferenceSettings>(SettingKeys.ProductReference, cancellationToken)
            ?? new ProductReferenceSettings();
        if (!config.IsEnabled)
            throw new BusinessRuleException("Mahsulot ma'lumotnomasi o'chirilgan.", "product_reference_disabled");
        if (config.SourceType != "GoogleSheets")
            throw new BusinessRuleException("Ma'lumotnoma manbasi qo'llab-quvvatlanmaydi.", "product_reference_source_unsupported");

        IReadOnlyList<ProductReferenceRow> rows;
        try
        {
            rows = await source.FetchAsync(config.ToSourceConfig(), cancellationToken);
        }
        catch (Exception ex)
        {
            config.LastError = ProductReferenceText.Clean(ex.Message, 500);
            config.LastErrorCount = 1;
            await settings.SetAsync(SettingKeys.ProductReference, config, cancellationToken);
            throw;
        }

        var now = DateTime.UtcNow;
        var errors = 0;
        var incoming = new Dictionary<string, ProductReferenceRow>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var barcode = ProductReferenceText.Clean(row.Barcode, 60);
            var name = ProductReferenceText.Clean(row.Name, 300);
            if (barcode.Length == 0 || name.Length == 0)
            {
                errors++;
                continue;
            }
            incoming[barcode] = row with { Barcode = barcode, Name = name };
        }

        var existing = await db.ProductReferences.ToDictionaryAsync(x => x.Barcode, StringComparer.Ordinal, cancellationToken);
        foreach (var (barcode, row) in incoming)
        {
            if (!existing.TryGetValue(barcode, out var entity))
            {
                entity = new Cartex.Domain.Entities.ProductReference { Barcode = barcode };
                db.ProductReferences.Add(entity);
            }
            entity.Name = row.Name!;
            entity.UnitHint = ProductReferenceText.Optional(row.UnitHint, 100);
            entity.CategoryHint = ProductReferenceText.Optional(row.CategoryHint, 150);
            entity.ManufacturerHint = ProductReferenceText.Optional(row.ManufacturerHint, 150);
            entity.PackQty = row.PackQty is > 0 ? row.PackQty : null;
            entity.SuggestedPrice = row.SuggestedPrice is >= 0 ? row.SuggestedPrice : null;
            entity.SourceKey = ProductReferenceText.Clean(row.SourceKey, 200);
            entity.SyncedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        config.LastSyncedAt = now;
        config.LastReadCount = rows.Count;
        config.LastUpdatedCount = incoming.Count;
        config.LastErrorCount = errors;
        config.RowCount = await db.ProductReferences.CountAsync(cancellationToken);
        config.LastError = null;
        await settings.SetAsync(SettingKeys.ProductReference, config, cancellationToken);
        return new ProductReferenceSyncResultDto(rows.Count, incoming.Count, errors, config.RowCount, now);
    }
}

internal static class ProductReferenceText
{
    public static string Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var cleaned = string.Concat(value.Where(character => !char.IsControl(character))).Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }

    public static string? Optional(string? value, int maxLength)
    {
        var cleaned = Clean(value, maxLength);
        return cleaned.Length == 0 ? null : cleaned;
    }
}
