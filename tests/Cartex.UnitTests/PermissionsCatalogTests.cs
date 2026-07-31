using Cartex.Domain.Authorization;
using Xunit;

namespace Cartex.UnitTests;

public class PermissionsCatalogTests
{
    [Fact]
    public void Wildcard_IsStar() => Assert.Equal("*", AppPermissions.Wildcard);

    [Fact]
    public void DeveloperOnly_ContainsGranularFeatureAndKeyPermissions()
    {
        Assert.Contains(AppPermissions.Features.View, AppPermissions.DeveloperOnly);
        Assert.Contains(AppPermissions.Features.Edit, AppPermissions.DeveloperOnly);
        Assert.Contains(AppPermissions.Keys.Create, AppPermissions.DeveloperOnly);
        Assert.Contains(AppPermissions.Keys.Revoke, AppPermissions.DeveloperOnly);
    }

    [Fact]
    public void Catalog_NotEmpty_AndExcludesWildcard()
    {
        Assert.NotEmpty(AppPermissions.Catalog);
        Assert.DoesNotContain(AppPermissions.Wildcard, AppPermissions.Catalog.Keys);
    }

    [Fact]
    public void Catalog_HasNoLegacyManagePermissions()
    {
        Assert.DoesNotContain(AppPermissions.Catalog.Keys, key =>
            key.EndsWith(".manage", StringComparison.Ordinal)
            || key.EndsWith(".manageAll", StringComparison.Ordinal));
    }

    [Fact]
    public void NotificationJournal_HasSeparateViewSensitiveAndExportPermissions()
    {
        Assert.Contains(AppPermissions.Notifications.JournalView, AppPermissions.Catalog.Keys);
        Assert.Contains(AppPermissions.Notifications.JournalSensitive, AppPermissions.Catalog.Keys);
        Assert.Contains(AppPermissions.Notifications.JournalExport, AppPermissions.Catalog.Keys);
        Assert.Contains(
            AppPermissions.Notifications.JournalView,
            AppPermissions.Definitions[AppPermissions.Notifications.JournalExport].DependsOn);
    }

    [Fact]
    public void DependencyGraph_ReferencesKnownPermissions_AndIsAcyclic()
    {
        foreach (var definition in AppPermissions.Definitions.Values)
            Assert.All(definition.DependsOn, dependency =>
                Assert.True(AppPermissions.Definitions.ContainsKey(dependency),
                    $"{definition.Key} depends on unknown permission {dependency}"));

        foreach (var permission in AppPermissions.Catalog.Keys)
            Assert.DoesNotContain(permission, PermissionDependencies.RequiredFor([permission]));
    }

    [Fact]
    public void Bundles_ExpandToClosedKnownPermissionSets()
    {
        foreach (var bundle in AppPermissions.Bundles.Values)
        {
            Assert.All(bundle.Permissions, permission =>
                Assert.True(AppPermissions.Definitions.ContainsKey(permission),
                    $"{bundle.Key} contains unknown permission {permission}"));

            var effective = PermissionDependencies.Effective(bundle.Permissions);
            Assert.All(effective, permission => Assert.Contains(permission, AppPermissions.Catalog.Keys));
        }
    }

    [Fact]
    public void DisablingProductView_DisablesProductEditingDependents()
    {
        var dependents = PermissionDependencies.DependentsOf(AppPermissions.Products.View);
        Assert.Contains(AppPermissions.Products.Create, dependents);
        Assert.Contains(AppPermissions.Products.Edit, dependents);
        Assert.Contains(AppPermissions.Supplies.Create, dependents);
    }
}
