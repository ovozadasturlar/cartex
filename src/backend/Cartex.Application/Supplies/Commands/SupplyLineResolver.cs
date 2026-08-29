using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Domain.Measurement;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

using Unit = Cartex.Domain.Entities.Unit;

namespace Cartex.Application.Supplies.Commands;

public sealed record ResolvedSupplyLine(decimal Quantity, decimal Price, decimal? SellingPrice, decimal PackSize);

public sealed class SupplyLineResolver
{
    private sealed record VariantInfo(long ProductId, Unit Unit);

    private readonly Dictionary<long, VariantInfo> _variants;
    private readonly Dictionary<long, Unit> _units;
    private readonly Dictionary<long, ProductPack> _packs;

    private SupplyLineResolver(Dictionary<long, VariantInfo> variants, Dictionary<long, Unit> units, Dictionary<long, ProductPack> packs)
    {
        _variants = variants;
        _units = units;
        _packs = packs;
    }

    public static async Task<SupplyLineResolver> LoadAsync(IApplicationDbContext db, List<CreateSupplyItemDto> items, CancellationToken cancellationToken)
    {
        var variantIds = items.Select(i => i.VariantId).Distinct().ToList();
        var variants = await db.ProductVariants
            .Where(v => variantIds.Contains(v.Id))
            .Select(v => new { v.Id, Info = new VariantInfo(v.ProductId, v.Product.Unit) })
            .ToDictionaryAsync(x => x.Id, x => x.Info, cancellationToken);

        var unitIds = items.Where(i => i.UnitId is not null).Select(i => i.UnitId!.Value).Distinct().ToList();
        var units = unitIds.Count == 0
            ? []
            : await db.Units.Where(u => unitIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, cancellationToken);

        var packIds = items.Where(i => i.PackId is not null).Select(i => i.PackId!.Value).Distinct().ToList();
        var packs = packIds.Count == 0
            ? []
            : await db.ProductPacks.Where(p => packIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);

        return new SupplyLineResolver(variants, units, packs);
    }

    public ResolvedSupplyLine Resolve(CreateSupplyItemDto item)
    {
        if (!_variants.TryGetValue(item.VariantId, out var variant))
            throw new NotFoundException("Mahsulot topilmadi.");

        if (item.UnitId is not null && item.PackId is not null)
            throw new BusinessRuleException("Bir qatorda birlik va qadoq birga tanlanmaydi.");

        Unit? entryUnit = null;
        if (item.UnitId is { } unitId && !_units.TryGetValue(unitId, out entryUnit))
            throw new NotFoundException("Birlik topilmadi.");

        var packSize = 1m;
        if (item.PackId is { } packId)
        {
            if (!_packs.TryGetValue(packId, out var pack))
                throw new NotFoundException("Qadoq topilmadi.");
            if (pack.ProductId != variant.ProductId)
                throw new BusinessRuleException("Qadoq boshqa mahsulotga tegishli.");
            if (pack.Kind == PackKind.Sale)
                throw new BusinessRuleException("Bu qadoq faqat sotuv uchun — kirimda ishlatilmaydi.");
            packSize = pack.Size;
        }

        var normalized = UnitConversion.Normalize(
            item.Quantity, item.PurchasePrice, entryUnit, variant.Unit, packSize, item.PriceBasis);

        return new ResolvedSupplyLine(normalized.Quantity, normalized.Price, item.SellingPrice, packSize);
    }
}
