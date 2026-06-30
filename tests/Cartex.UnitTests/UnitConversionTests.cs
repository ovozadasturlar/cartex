using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Domain.Measurement;
using Xunit;

namespace Cartex.UnitTests;

public class UnitConversionTests
{
    private static Unit U(string shortName, UnitDimension dim, decimal factor)
        => new() { Name = shortName, ShortName = shortName, Dimension = dim, Factor = factor };

    private static readonly Unit Gram = U("g", UnitDimension.Weight, 1);
    private static readonly Unit Kg = U("kg", UnitDimension.Weight, 1000);
    private static readonly Unit Tonne = U("t", UnitDimension.Weight, 1_000_000);
    private static readonly Unit Litre = U("l", UnitDimension.Volume, 1000);

    [Fact]
    public void Tonne_To_KgStocking() => Assert.Equal(2000m, UnitConversion.ToBase(2m, Tonne, Kg));

    [Fact]
    public void Gram_To_KgStocking() => Assert.Equal(0.5m, UnitConversion.ToBase(500m, Gram, Kg));

    [Fact]
    public void SameUnit_NoChange() => Assert.Equal(3m, UnitConversion.ToBase(3m, Kg, Kg));

    [Fact]
    public void Price_PerKg_To_PerGram() => Assert.Equal(5m, UnitConversion.PricePerBase(5000m, Kg, Gram));

    [Fact]
    public void DimensionMismatch_Throws() =>
        Assert.Throws<BusinessRuleException>(() => UnitConversion.ToBase(1m, Litre, Kg));
}
