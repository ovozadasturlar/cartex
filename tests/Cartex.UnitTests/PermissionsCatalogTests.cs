using Cartex.Domain.Authorization;
using Xunit;

namespace Cartex.UnitTests;

public class PermissionsCatalogTests
{
    [Fact]
    public void Wildcard_IsStar() => Assert.Equal("*", AppPermissions.Wildcard);

    [Fact]
    public void DeveloperOnly_ContainsManagePermissions()
    {
        Assert.Contains(AppPermissions.Features.Manage, AppPermissions.DeveloperOnly);
        Assert.Contains(AppPermissions.Keys.Manage, AppPermissions.DeveloperOnly);
    }

    [Fact]
    public void Catalog_NotEmpty_AndExcludesWildcard()
    {
        Assert.NotEmpty(AppPermissions.Catalog);
        Assert.DoesNotContain(AppPermissions.Wildcard, AppPermissions.Catalog.Keys);
    }
}
