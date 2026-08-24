using Cartex.Application.Features.Commands;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common.Exceptions;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public sealed class OwnerModuleLicenseTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task SOZ_08a_Owner_cannot_enable_an_unlicensed_module()
    {
        await SetStateAsync(isEnabled: false, ownerEnabled: false);

        using var scope = Fixture.CreateScope();
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => scope.ServiceProvider
            .GetRequiredService<ISender>()
            .Send(new SetOwnerModuleCommand(FeatureCatalog.Ordering, true)));

        Assert.Equal("module_not_licensed", error.Code);
        Assert.Equal((false, false), await StateAsync());
    }

    [Fact]
    public async Task SOZ_08a_Disabling_license_also_disables_owner_switch()
    {
        await SetStateAsync(isEnabled: true, ownerEnabled: true);

        using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new SetFeatureCommand(FeatureCatalog.Ordering, false));

        Assert.Equal((false, false), await StateAsync());
    }

    [Fact]
    public async Task SOZ_08a_Enabling_license_does_not_enable_owner_switch()
    {
        await SetStateAsync(isEnabled: false, ownerEnabled: false);

        using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new SetFeatureCommand(FeatureCatalog.Ordering, true));

        Assert.Equal((true, false), await StateAsync());
    }

    private async Task SetStateAsync(bool isEnabled, bool ownerEnabled)
    {
        using var scope = Fixture.CreateScope();
        var feature = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Features.SingleAsync(x => x.Code == FeatureCatalog.Ordering);
        feature.IsEnabled = isEnabled;
        feature.OwnerEnabled = ownerEnabled;
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().SaveChangesAsync();
    }

    private async Task<(bool IsEnabled, bool OwnerEnabled)> StateAsync()
    {
        using var scope = Fixture.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Features.Where(x => x.Code == FeatureCatalog.Ordering)
            .Select(x => new ValueTuple<bool, bool>(x.IsEnabled, x.OwnerEnabled))
            .SingleAsync();
    }
}
