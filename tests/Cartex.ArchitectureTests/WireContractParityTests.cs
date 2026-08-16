using System.Reflection;
using Cartex.Application.Ordering.Commands;
using Cartex.Application.Sales.Commands;
using Cartex.Shared.Models.Ordering;
using Cartex.Shared.Models.Sales;
using Xunit;

namespace Cartex.ArchitectureTests;

/// The cart pipe is the one place a field can vanish without anybody noticing: the typed clients
/// post a request record, the server binds or maps it onto a command, and a name that exists on
/// only one side is simply ignored. A dropped discount is money, so it is checked, not trusted.
public class WireContractParityTests
{
    private static readonly (Type Request, Type Command)[] Mapped =
    [
        (typeof(SubmitCartRequest), typeof(SubmitCartCommand)),
        (typeof(UpdateCartRequest), typeof(UpdateCartCommand)),
        (typeof(CheckoutCartRequest), typeof(CheckoutCartCommand)),
        (typeof(CreateSaleRequest), typeof(CreateSaleCommand)),
    ];

    public static TheoryData<Type, Type> Pairs
    {
        get
        {
            var data = new TheoryData<Type, Type>();
            foreach (var (request, command) in Mapped) data.Add(request, command);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void Every_request_field_has_a_home_on_the_command(Type request, Type command)
    {
        var handled = Names(command);
        var orphans = Names(request).Where(name => !handled.Contains(name)).ToList();

        Assert.True(orphans.Count == 0,
            $"{request.Name} sends {string.Join(", ", orphans)}, but {command.Name} has nowhere to put it.");
    }

    [Theory]
    [InlineData(nameof(UpdateCartRequest), "Update")]
    [InlineData(nameof(CheckoutCartRequest), "Checkout")]
    public void Every_request_field_is_copied_by_the_controller(string requestName, string action)
    {
        var request = Mapped.First(pair => pair.Request.Name == requestName).Request;
        var body = ActionBody(action);
        var dropped = Names(request).Where(name => !body.Contains($"request.{name}")).ToList();

        Assert.True(dropped.Count == 0,
            $"OrderingController.{action} never reads {string.Join(", ", dropped)} off the request.");
    }

    private static HashSet<string> Names(Type type) =>
        [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name != "EqualityContract")
            .Select(p => p.Name)];

    private static string ActionBody(string action)
    {
        var source = File.ReadAllText(Path.Combine(
            SolutionRoot.Find(), "src", "backend", "Cartex.Api", "Controllers", "OrderingController.cs"));
        var start = source.IndexOf($" {action}(string code", StringComparison.Ordinal);
        Assert.True(start > 0, $"OrderingController.{action} was renamed; update this test.");
        var next = source.IndexOf("\n    [Http", start, StringComparison.Ordinal);
        return next < 0 ? source[start..] : source[start..next];
    }
}
