using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Entities;

namespace Cartex.Domain.Measurement;

public static class UnitConversion
{
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
