using Cartex.Mobile.Core;
using Cartex.Shared.Models.Settings;
using Xunit;

namespace Cartex.UnitTests;

public sealed class AccessStateCapabilityTests
{
    [Fact]
    public async Task RUXSAT_04a_CanUseCart_is_false_when_ordering_and_store_are_disabled()
    {
        var state = await LoadedStateAsync(
            ["sales.create", "sales.pick"],
            [],
            new SalesPolicyDto { AllowSaleQueue = true });

        Assert.False(state.CanUseCart);
    }

    [Fact]
    public async Task RUXSAT_04a_CanSell_is_false_when_permission_exists_but_sales_module_is_disabled()
    {
        var state = await LoadedStateAsync(["sales.create"], ["supplies"]);

        Assert.False(state.CanSell);
    }

    [Fact]
    public async Task RUXSAT_04a_CanUseCart_is_false_when_sales_module_is_enabled_but_permissions_are_missing()
    {
        var state = await LoadedStateAsync([], ["ordering"]);

        Assert.False(state.CanUseCart);
    }

    [Fact]
    public async Task RUXSAT_04a_CanReceiveStock_is_false_when_supplies_module_is_disabled()
    {
        var state = await LoadedStateAsync(["supplies.create"], ["ordering"]);

        Assert.False(state.CanReceiveStock);
    }

    [Fact]
    public async Task RUXSAT_04a_CanQueue_is_false_when_queue_policy_is_disabled()
    {
        var state = await LoadedStateAsync(
            ["sales.pick"],
            ["ordering"],
            new SalesPolicyDto { AllowSaleQueue = false });

        Assert.False(state.CanQueue);
    }

    private static async Task<AccessState> LoadedStateAsync(
        string[] permissions,
        string[] features,
        SalesPolicyDto? policy = null)
    {
        var snapshot = new AccessSnapshot(
            permissions.ToHashSet(StringComparer.Ordinal),
            features.ToHashSet(StringComparer.OrdinalIgnoreCase),
            policy ?? new SalesPolicyDto(),
            DateTimeOffset.UtcNow);
        var state = new AccessState(new FakeLoader(snapshot));

        await state.EnsureLoadedAsync(TestContext.Current.CancellationToken);

        return state;
    }

    private sealed class FakeLoader(AccessSnapshot snapshot) : IAccessStateLoader
    {
        public bool IsOnline => true;

        public event Action? ConnectivityChanged
        {
            add { }
            remove { }
        }

        public Task<AccessSnapshot> LoadAsync(bool refresh, CancellationToken cancellationToken) =>
            Task.FromResult(snapshot);

        public void Clear() { }
    }
}
