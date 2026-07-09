using Cartex.Application.Common.Loyalty;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Xunit;

namespace Cartex.UnitTests;

public class DiscountEngineTests
{
    private static readonly DateOnly Today = new(2026, 7, 9);

    private static DiscountRule Rule(string name, DiscountScope scope = DiscountScope.All, long? targetId = null,
        decimal value = 10, DiscountMethod method = DiscountMethod.Percent, int priority = 0,
        decimal minAmount = 0, long? customerId = null, DateOnly? startsOn = null, DateOnly? endsOn = null,
        params long[] exceptions) =>
        new()
        {
            Name = name,
            IsEnabled = true,
            Scope = scope,
            TargetId = targetId,
            CustomerId = customerId,
            MinAmount = minAmount,
            Method = method,
            Value = value,
            Priority = priority,
            StartsOn = startsOn,
            EndsOn = endsOn,
            Exceptions = exceptions.Select(p => new DiscountRuleException { ProductId = p }).ToList()
        };

    private static DiscountLine Line(long productId, decimal total, long? categoryId = null, long? manufacturerId = null) =>
        new(productId, categoryId, manufacturerId, total);

    [Fact]
    public void Priority_mode_applies_only_highest_priority_rule()
    {
        var rules = new[] { Rule("A", value: 5, priority: 1), Rule("B", value: 10, priority: 2) };
        var applied = DiscountEngine.Compute(rules, 0, DiscountCombineMode.Priority, null, Today, [Line(1, 100_000)]);
        var only = Assert.Single(applied);
        Assert.Equal("B", only.Name);
        Assert.Equal(10_000, only.Amount);
    }

    [Fact]
    public void Stack_mode_sums_all_matching_rules()
    {
        var rules = new[] { Rule("A", value: 5), Rule("B", value: 10) };
        var applied = DiscountEngine.Compute(rules, 0, DiscountCombineMode.Stack, null, Today, [Line(1, 100_000)]);
        Assert.Equal(2, applied.Count);
        Assert.Equal(15_000, applied.Sum(a => a.Amount));
    }

    [Fact]
    public void MinAmount_threshold_gates_rule()
    {
        var rules = new[] { Rule("Aksiya", scope: DiscountScope.Category, targetId: 7, minAmount: 200_000, value: 20_000, method: DiscountMethod.FixedAmount) };
        var below = DiscountEngine.Compute(rules, 0, DiscountCombineMode.Priority, null, Today, [Line(1, 150_000, categoryId: 7)]);
        Assert.Empty(below);
        var above = DiscountEngine.Compute(rules, 0, DiscountCombineMode.Priority, null, Today, [Line(1, 250_000, categoryId: 7)]);
        Assert.Equal(20_000, Assert.Single(above).Amount);
    }

    [Fact]
    public void Exceptions_exclude_products_from_scope()
    {
        var rules = new[] { Rule("Hammasi", value: 10, exceptions: 2) };
        var applied = DiscountEngine.Compute(rules, 0, DiscountCombineMode.Priority, null, Today,
            [Line(1, 100_000), Line(2, 50_000)]);
        Assert.Equal(10_000, Assert.Single(applied).Amount);
    }

    [Fact]
    public void Customer_rule_only_applies_to_that_customer()
    {
        var rules = new[] { Rule("VIP", value: 10, customerId: 5) };
        Assert.Empty(DiscountEngine.Compute(rules, 0, DiscountCombineMode.Priority, 9, Today, [Line(1, 100_000)]));
        Assert.Single(DiscountEngine.Compute(rules, 0, DiscountCombineMode.Priority, 5, Today, [Line(1, 100_000)]));
    }

    [Fact]
    public void Customer_percent_wins_in_priority_mode()
    {
        var rules = new[] { Rule("Umumiy", value: 20, priority: 50) };
        var applied = DiscountEngine.Compute(rules, 5, DiscountCombineMode.Priority, 3, Today, [Line(1, 100_000)]);
        var only = Assert.Single(applied);
        Assert.Equal("customer", only.Name);
        Assert.Equal(5_000, only.Amount);
    }

    [Fact]
    public void Date_window_is_respected()
    {
        var expired = new[] { Rule("Yangi yil", value: 10, startsOn: new DateOnly(2025, 12, 1), endsOn: new DateOnly(2025, 12, 31)) };
        Assert.Empty(DiscountEngine.Compute(expired, 0, DiscountCombineMode.Priority, null, Today, [Line(1, 100_000)]));

        var active = new[] { Rule("Yoz", value: 10, startsOn: new DateOnly(2026, 7, 1), endsOn: new DateOnly(2026, 7, 31)) };
        Assert.Single(DiscountEngine.Compute(active, 0, DiscountCombineMode.Priority, null, Today, [Line(1, 100_000)]));
    }

    [Fact]
    public void Manufacturer_scope_matches_lines()
    {
        var rules = new[] { Rule("Brend", scope: DiscountScope.Manufacturer, targetId: 4, value: 10) };
        var applied = DiscountEngine.Compute(rules, 0, DiscountCombineMode.Priority, null, Today,
            [Line(1, 100_000, manufacturerId: 4), Line(2, 50_000, manufacturerId: 9)]);
        Assert.Equal(10_000, Assert.Single(applied).Amount);
    }

    [Fact]
    public void Total_discount_never_exceeds_subtotal()
    {
        var rules = new[]
        {
            Rule("Katta", value: 90_000, method: DiscountMethod.FixedAmount),
            Rule("Yana", value: 50_000, method: DiscountMethod.FixedAmount)
        };
        var applied = DiscountEngine.Compute(rules, 0, DiscountCombineMode.Stack, null, Today, [Line(1, 100_000)]);
        Assert.True(applied.Sum(a => a.Amount) <= 100_000);
    }

    [Fact]
    public void Disabled_rule_is_ignored()
    {
        var rule = Rule("Off", value: 10);
        rule.IsEnabled = false;
        Assert.Empty(DiscountEngine.Compute([rule], 0, DiscountCombineMode.Priority, null, Today, [Line(1, 100_000)]));
    }
}
