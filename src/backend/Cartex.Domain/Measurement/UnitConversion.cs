using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;

namespace Cartex.Domain.Measurement;

// Kiritilgan miqdor va narx (birlik yoki qadoq bo'yicha) saqlash birligiga keltiriladi.
public readonly record struct NormalizedLine(decimal Quantity, decimal Price);

public static class UnitConversion
{
    /// <summary>
    /// Yagona normalizatsiya nuqtasi: kirim "0.5 t", "10 qop" yoki "500 kg" bo'lishidan qat'i nazar
    /// natija saqlash birligida chiqadi. Narx PerEntry bo'lsa kiritilgan birlik/qadoq uchun, aks holda
    /// allaqachon saqlash birligi uchun deb qabul qilinadi.
    /// Muhim invariant: kiritilgan pul (miqdor × narx) o'zgarmaydi.
    /// </summary>
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
