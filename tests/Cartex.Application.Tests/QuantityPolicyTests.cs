using Cartex.Application.Common.Measurement;
using System.Globalization;
using Xunit;

namespace Cartex.Application.Tests;

public class QuantityPolicyTests
{
    [Theory]
    [InlineData("1", "1")]
    [InlineData("50", "1")]
    [InlineData("0.5", "0.5")]
    [InlineData("1.125", "0.001")]
    public void Quantity_matching_the_effective_step_is_valid(string quantity, string step)
    {
        Assert.True(QuantityPolicyService.IsValid(Parse(quantity), Parse(step)));
    }

    [Theory]
    [InlineData("1.5", "1")]
    [InlineData("0.25", "0.5")]
    [InlineData("1.0001", "0.001")]
    [InlineData("0", "1")]
    [InlineData("-1", "1")]
    public void Quantity_not_matching_the_effective_step_is_rejected(string quantity, string step)
    {
        Assert.False(QuantityPolicyService.IsValid(Parse(quantity), Parse(step)));
    }

    [Fact]
    public void Zero_is_only_valid_for_commands_that_explicitly_allow_it()
    {
        Assert.True(QuantityPolicyService.IsValid(0m, 1m, allowZero: true));
        Assert.False(QuantityPolicyService.IsValid(0m, 1m));
    }

    private static decimal Parse(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);
}
