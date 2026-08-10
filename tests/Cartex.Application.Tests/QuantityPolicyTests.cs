using Cartex.Application.Common.Measurement;
using Cartex.Domain.Common.Exceptions;
using System.Globalization;
using Xunit;

namespace Cartex.Application.Tests;

public class QuantityPolicyTests
{
    [Theory]
    [InlineData("1", true)]
    [InlineData("50", true)]
    [InlineData("0.5", true)]
    [InlineData("1.125", true)]
    [InlineData("0.001", true)]
    [InlineData("1", false)]
    [InlineData("50", false)]
    public void Valid_quantities_are_accepted(string quantity, bool allowFractional)
    {
        Assert.True(QuantityPolicyService.IsValid(Parse(quantity), allowFractional));
    }

    [Theory]
    [InlineData("1.5", false)]
    [InlineData("0.25", false)]
    [InlineData("1.0001", true)]
    [InlineData("1.0001", false)]
    [InlineData("0", true)]
    [InlineData("-1", true)]
    [InlineData("0", false)]
    [InlineData("-1", false)]
    public void Invalid_quantities_are_rejected(string quantity, bool allowFractional)
    {
        Assert.False(QuantityPolicyService.IsValid(Parse(quantity), allowFractional));
    }

    [Fact]
    public void Zero_is_only_valid_for_commands_that_explicitly_allow_it()
    {
        Assert.True(QuantityPolicyService.IsValid(0m, true, allowZero: true));
        Assert.False(QuantityPolicyService.IsValid(0m, true));
        Assert.False(QuantityPolicyService.IsValid(0m, false));
    }

    [Fact]
    public void More_than_three_decimals_raise_precision_error()
    {
        var ex = Assert.Throws<BusinessRuleException>(() => QuantityPolicyService.EnsureValid(1.0001m, true));
        Assert.Equal("quantity_precision_exceeded", ex.Code);
    }

    [Fact]
    public void Fraction_for_whole_number_unit_raises_whole_required_error()
    {
        var ex = Assert.Throws<BusinessRuleException>(() => QuantityPolicyService.EnsureValid(1.5m, false));
        Assert.Equal("quantity_whole_required", ex.Code);
    }

    private static decimal Parse(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);
}
