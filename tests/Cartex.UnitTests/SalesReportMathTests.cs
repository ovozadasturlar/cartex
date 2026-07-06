using Cartex.Application.Reports.Queries;
using Xunit;

namespace Cartex.UnitTests;

public class SalesReportMathTests
{
    [Fact]
    public void DiscountRate_is_zero_when_gross_is_zero()
    {
        Assert.Equal(0m, SalesReportMath.DiscountRate(0m, 100m));
    }

    [Theory]
    [InlineData(1000, 100, 0.1)]
    [InlineData(2000, 500, 0.25)]
    [InlineData(1000, 0, 0)]
    public void DiscountRate_is_discount_over_gross(decimal gross, decimal discount, decimal expected)
    {
        Assert.Equal(expected, SalesReportMath.DiscountRate(gross, discount));
    }

    [Fact]
    public void NetRevenue_subtracts_returned_quantity()
    {
        Assert.Equal(600m, SalesReportMath.NetRevenue(5m, 2m, 200m, 0m));
    }

    [Fact]
    public void NetRevenue_applies_discount_rate()
    {
        Assert.Equal(900m, SalesReportMath.NetRevenue(5m, 0m, 200m, 0.1m));
    }

    [Fact]
    public void NetRevenue_is_zero_when_all_returned()
    {
        Assert.Equal(0m, SalesReportMath.NetRevenue(3m, 3m, 200m, 0m));
    }

    [Fact]
    public void Profit_accounts_for_discount_and_returns()
    {
        Assert.Equal(240m, SalesReportMath.Profit(5m, 1m, 200m, 120m, 0.1m));
    }

    [Fact]
    public void Profit_can_be_negative_when_discount_exceeds_margin()
    {
        Assert.Equal(-20m, SalesReportMath.Profit(1m, 0m, 100m, 120m, 0m));
    }

    [Fact]
    public void Profit_is_zero_when_all_returned()
    {
        Assert.Equal(0m, SalesReportMath.Profit(4m, 4m, 200m, 120m, 0.1m));
    }
}
