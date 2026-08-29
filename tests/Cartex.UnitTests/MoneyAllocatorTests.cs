using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Pricing;
using Xunit;

namespace Cartex.UnitTests;

public class MoneyAllocatorTests
{
    private static decimal[] Big(int count) => [.. Enumerable.Repeat(1_000_000m, count)];

    [Fact]
    public void Thirds_sum_back_to_the_whole()
    {
        var result = MoneyAllocator.Distribute(10m, [1m, 1m, 1m], Big(3));

        Assert.Equal(10m, result.Placed.Sum());
        Assert.Equal(0m, result.Residual);
        Assert.Equal([3.34m, 3.33m, 3.33m], result.Placed);
    }

    [Fact]
    public void Uneven_weights_sum_exactly()
    {
        var lines = new[] { 33_333m, 33_333m, 33_334m };

        var result = MoneyAllocator.Distribute(10_000m, lines, lines);

        Assert.Equal(10_000m, result.Placed.Sum());
        Assert.Equal(0m, result.Residual);
    }

    [Fact]
    public void A_line_never_receives_more_than_its_capacity()
    {
        var result = MoneyAllocator.Distribute(10m, [1m, 1m], [1m, 100m]);

        Assert.Equal([1m, 9m], result.Placed);
        Assert.Equal(0m, result.Residual);
    }

    [Fact]
    public void Amount_beyond_total_capacity_comes_back_as_residual()
    {
        var result = MoneyAllocator.Distribute(10m, [1m, 1m], [2m, 3m]);

        Assert.Equal(5m, result.Placed.Sum());
        Assert.Equal(5m, result.Residual);
    }

    [Fact]
    public void Zero_weights_fall_back_to_capacity()
    {
        var result = MoneyAllocator.Distribute(8m, [0m, 0m], [10m, 30m]);

        Assert.Equal([2m, 6m], result.Placed);
        Assert.Equal(0m, result.Residual);
    }

    [Fact]
    public void Placed_plus_residual_always_equals_the_amount()
    {
        decimal[] weights = [7m, 0m, 13.5m, 1m];
        decimal[] capacities = [10m, 5m, 0.03m, 40m];

        foreach (var amount in new[] { 0m, 0.01m, 1m, 33.33m, 54.03m, 100m })
        {
            var result = MoneyAllocator.Distribute(amount, weights, capacities);

            Assert.Equal(amount, result.Placed.Sum() + result.Residual);
            for (var i = 0; i < capacities.Length; i++)
            {
                Assert.True(result.Placed[i] >= 0);
                Assert.True(result.Placed[i] <= capacities[i]);
            }
        }
    }

    [Fact]
    public void Ties_break_by_index_so_the_split_is_reproducible()
    {
        var first = MoneyAllocator.Distribute(1m, [1m, 1m, 1m], Big(3));
        var second = MoneyAllocator.Distribute(1m, [1m, 1m, 1m], Big(3));

        Assert.Equal(first.Placed, second.Placed);
        Assert.Equal([0.34m, 0.33m, 0.33m], first.Placed);
    }

    [Fact]
    public void Zero_amount_places_nothing()
    {
        var result = MoneyAllocator.Distribute(0m, [1m, 1m], Big(2));

        Assert.Equal([0m, 0m], result.Placed);
        Assert.Equal(0m, result.Residual);
    }

    [Fact]
    public void Negative_amount_is_rejected() =>
        Assert.Throws<BusinessRuleException>(() => MoneyAllocator.Distribute(-1m, [1m], Big(1)));

    [Fact]
    public void Mismatched_input_lengths_are_rejected() =>
        Assert.Throws<BusinessRuleException>(() => MoneyAllocator.Distribute(1m, [1m, 1m], Big(1)));
}
