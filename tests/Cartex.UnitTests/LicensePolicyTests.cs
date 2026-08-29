using Cartex.Domain.Authorization;
using Xunit;

namespace Cartex.UnitTests;

public class LicensePolicyTests
{
    [Fact]
    public void FreeMode_AllowsCoreWriteOperations()
    {
        Assert.Contains(AppPermissions.Sales.Create, LicensePolicy.FreeModeAllowed);
        Assert.Contains(AppPermissions.Supplies.Create, LicensePolicy.FreeModeAllowed);
        Assert.Contains(AppPermissions.Customers.Edit, LicensePolicy.FreeModeAllowed);
    }

    [Fact]
    public void FreeMode_BlocksAnalyticsAndDebtViewing()
    {
        Assert.DoesNotContain(AppPermissions.Reports.View, LicensePolicy.FreeModeAllowed);
        Assert.DoesNotContain(AppPermissions.Transactions.View, LicensePolicy.FreeModeAllowed);
        Assert.DoesNotContain(AppPermissions.Accounts.View, LicensePolicy.FreeModeAllowed);
    }

    [Fact]
    public void Unknown_tariff_fails_closed_to_free()
    {
        Assert.Equal(TariffCatalog.FeaturesFor(TariffCatalog.Free), TariffCatalog.FeaturesFor("bogus"));
        Assert.Equal(TariffCatalog.FeaturesFor(TariffCatalog.Free), TariffCatalog.FeaturesFor(null));
        Assert.DoesNotContain(FeatureCatalog.Reports, TariffCatalog.FeaturesFor("bogus"));
    }

    [Fact]
    public void Ordering_is_disabled_by_default()
    {
        Assert.Contains(FeatureCatalog.Ordering, FeatureCatalog.DefaultDisabled);
    }
}
