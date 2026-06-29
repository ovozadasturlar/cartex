using Cartex.Domain.Authorization;
using Xunit;

namespace Cartex.UnitTests;

public class FeatureCatalogTests
{
    [Fact]
    public void PermissionsFor_DisabledReports_RemovesReportsView()
    {
        var perms = FeatureCatalog.PermissionsFor([FeatureCatalog.Reports]);
        Assert.Contains(AppPermissions.Reports.View, perms);
    }

    [Fact]
    public void PermissionsFor_CommunicationCodes_AreEmpty()
    {
        var perms = FeatureCatalog.PermissionsFor([FeatureCatalog.Telegram, FeatureCatalog.Email, FeatureCatalog.Sms, FeatureCatalog.Ordering]);
        Assert.Empty(perms);
    }

    [Fact]
    public void PermissionsFor_UnknownCode_ReturnsEmpty()
    {
        Assert.Empty(FeatureCatalog.PermissionsFor(["does-not-exist"]));
    }

    [Fact]
    public void EveryMappedCode_HasName()
    {
        foreach (var code in FeatureCatalog.Map.Keys)
            Assert.True(FeatureCatalog.Names.ContainsKey(code), $"Missing name for feature '{code}'");
    }
}
