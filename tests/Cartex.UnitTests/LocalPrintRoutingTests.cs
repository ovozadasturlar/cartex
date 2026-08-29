using Cartex.Shared.Models.Printing;
using Cartex.Shared.Models.Settings;
using Cartex.UI.Services;
using Xunit;

namespace Cartex.UnitTests;

public sealed class LocalPrintRoutingTests
{
    [Fact]
    public void CHOP_01_LocalFirst_with_a_local_printer_uses_local_printing()
    {
        var policy = Policy(PrintRoutingMode.LocalFirst, isEnabled: true);

        Assert.True(LocalPrintRouting.ShouldPrintLocally(policy, canPrintLocally: true,
            hasLocalPrinter: true, salesPolicyAllows: true));
    }

    [Fact]
    public void CHOP_01_PriorityOnly_uses_the_server()
    {
        var policy = Policy(PrintRoutingMode.PriorityOnly, isEnabled: true);

        Assert.False(LocalPrintRouting.ShouldPrintLocally(policy, canPrintLocally: true,
            hasLocalPrinter: true, salesPolicyAllows: true));
    }

    [Fact]
    public void CHOP_02_Missing_policy_cache_uses_the_server()
    {
        Assert.False(LocalPrintRouting.ShouldPrintLocally(null, canPrintLocally: true,
            hasLocalPrinter: true, salesPolicyAllows: true));
    }

    [Fact]
    public void CHOP_01_Disabled_policy_uses_the_server()
    {
        var policy = Policy(PrintRoutingMode.LocalFirst, isEnabled: false);

        Assert.False(LocalPrintRouting.ShouldPrintLocally(policy, canPrintLocally: true,
            hasLocalPrinter: true, salesPolicyAllows: true));
    }

    [Fact]
    public void CHOP_10_LocalOnly_prints_locally_only_on_the_selected_printers_machine()
    {
        var owner = Policy(PrintRoutingMode.LocalOnly, isEnabled: true, pinnedToThisDevice: true);
        var other = Policy(PrintRoutingMode.LocalOnly, isEnabled: true);

        Assert.True(LocalPrintRouting.ShouldPrintLocally(owner, canPrintLocally: true,
            hasLocalPrinter: true, salesPolicyAllows: true));
        Assert.False(LocalPrintRouting.ShouldPrintLocally(other, canPrintLocally: true,
            hasLocalPrinter: true, salesPolicyAllows: true));
    }

    private static LocalPrintPolicy Policy(
        PrintRoutingMode mode,
        bool isEnabled,
        bool pinnedToThisDevice = false) =>
        new(new PrintRoutingPolicyDto(1, 1, PrintJobKind.Receipt, isEnabled, mode, true,
                PrintStickyMode.Disabled, 0, null, null, 10, 30, 60, 20, []),
            new SalesPolicyDto(),
            HasEnabledLocalEndpoint: true,
            PinnedToThisDevice: pinnedToThisDevice);
}
