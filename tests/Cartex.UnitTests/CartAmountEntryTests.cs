using Cartex.UI.ViewModels;
using Xunit;

namespace Cartex.UnitTests;

public sealed class CartAmountEntryTests
{
    private static CartItem Item(decimal unitPrice = 30_000m, bool fractional = true) => new()
    {
        UnitPrice = unitPrice,
        OriginalPrice = unitPrice,
        AllowsAmountEntry = true,
        AllowsFractional = fractional
    };

    [Fact]
    public void Entered_amount_sets_quantity_and_is_not_rewritten()
    {
        var item = Item();

        item.AmountInput = 10_000m;

        Assert.Equal(0.333m, item.Quantity);
        Assert.Equal(10_000m, item.AmountInput);
        Assert.Equal(9_990m, item.LineTotal);
    }

    [Fact]
    public void Changing_quantity_refreshes_the_amount()
    {
        var item = Item();
        item.AmountInput = 10_000m;

        item.Quantity = 2m;

        Assert.Equal(60_000m, item.AmountInput);
    }

    [Fact]
    public void Changing_price_refreshes_the_amount()
    {
        var item = Item();
        item.AmountInput = 10_000m;

        item.UnitPrice = 20_000m;

        Assert.Equal(6_660m, item.AmountInput);
    }

    [Fact]
    public void Amount_below_the_previous_line_total_is_kept()
    {
        var item = Item();
        item.AmountInput = 10_000m;

        item.AmountInput = 9_990m;

        Assert.Equal(0.333m, item.Quantity);
        Assert.Equal(9_990m, item.AmountInput);
    }

    [Fact]
    public void Whole_unit_products_floor_to_whole_quantities()
    {
        var item = Item(fractional: false);

        item.AmountInput = 100_000m;

        Assert.Equal(3m, item.Quantity);
        Assert.Equal(100_000m, item.AmountInput);
    }
}
