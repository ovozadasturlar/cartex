using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;

namespace Cartex.Domain.Measurement;

public readonly record struct NormalizedLine(decimal Quantity, decimal Price);

public static class UnitConversion
{
    public static NormalizedLine Normalize(
        decimal quantity, decimal price, Unit? entryUnit, Unit stockingUnit, decimal packSize, SupplyPriceBasis basis)
    {
        if (packSize <= 0)
            throw new BusinessRuleException("Qadoq hajmi noldan katta bo'lishi kerak.");

        var ratio = packSize;
        if (entryUnit is not null)
        {
            EnsureSameDimension(entryUnit, stockingUnit);
            ratio *= entryUnit.Factor / stockingUnit.Factor;
        }

        var stockingQuantity = quantity * ratio;
        var stockingPrice = basis == SupplyPriceBasis.PerStockingUnit ? price : price / ratio;
        return new NormalizedLine(stockingQuantity, stockingPrice);
    }

    public static decimal ToBase(decimal quantity, Unit from, Unit stocking)
    {
        EnsureSameDimension(from, stocking);
        return quantity * from.Factor / stocking.Factor;
    }

    public static decimal PricePerBase(decimal pricePerFrom, Unit from, Unit stocking)
    {
        EnsureSameDimension(from, stocking);
        return pricePerFrom * stocking.Factor / from.Factor;
    }

    private static void EnsureSameDimension(Unit from, Unit stocking)
    {
        if (from.Dimension != stocking.Dimension)
            throw new BusinessRuleException(
                $"Birlik o'lchovi mos kelmaydi: {from.ShortName} ({from.Dimension}) ↔ {stocking.ShortName} ({stocking.Dimension}).");
    }
}
