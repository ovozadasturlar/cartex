using System.Reflection;
using Cartex.Application.Common.Settings;
using Cartex.Shared.Models.Settings;
using Xunit;

namespace Cartex.ArchitectureTests;

/// SOZ-05: a setting nobody can change is a defect. The stored class and the wire shape are two
/// hand-written field lists, so a new setting is one forgotten line away from being saved nowhere.
public class SalesPolicyContractTests
{
    [Fact]
    public void The_stored_policy_and_the_wire_policy_hold_the_same_fields()
    {
        var stored = Names(typeof(SalesPolicySettings));
        var wire = Names(typeof(SalesPolicyDto));

        Assert.Equal(stored.OrderBy(x => x), wire.OrderBy(x => x));
    }

    [Fact]
    public void Every_field_survives_a_round_trip()
    {
        var cfg = new SalesPolicySettings();
        var changed = new SalesPolicyDto
        {
            ShiftPolicy = "AllSales",
            MaxDiscountPercent = 11,
            MaxDebtWriteOffAmount = 13,
            MaxDebtWriteOffPercent = 14,
            DefaultMinStock = 15,
            StaleRateDays = 16,
            AllowDebtSales = false,
            AllowCustomerCredit = true,
            RequireDebtDueDate = false,
            RequireSupplier = true,
            ShowOutOfStock = true,
            ShowUnlistedProducts = false,
            AllowInsufficientStockSales = true,
            AllowRetroactiveCashback = true,
            SaleCorrectionWindow = "Days",
            SaleCorrectionDays = 17,
            UpdateCatalogPriceOnSale = false,
            MaxPriceIncreasePercent = 18,
            CustomerRequirement = "Always",
            AllowReturnOnVoidedSale = true,
            AllowFreeReturnLines = false,
            RequireReturnReason = true
        };

        SalesPolicyMapping.Apply(cfg, changed);

        // Every value above differs from the default, so a field the mapping forgets comes back
        // as its default and the comparison names it.
        Assert.Equal(changed, SalesPolicyMapping.ToDto(cfg));
    }

    private static IEnumerable<string> Names(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name != "EqualityContract")
            .Select(p => p.Name);
}
