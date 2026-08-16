using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Cartex.UI.Views;
using Xunit;

namespace Cartex.UiTests;

/// A view that compiles can still fail the moment it is shown: a binding path that does not exist,
/// a template without its data type, a resource key that was renamed. The compiler sees none of
/// that. These tests build each screen the way the app does and fail on any binding error, so a
/// broken screen is caught here instead of in front of a cashier.
public class ViewRenderTests
{
    [AvaloniaTheory]
    [InlineData(typeof(ModulesView))]
    [InlineData(typeof(SalesPolicyView))]
    [InlineData(typeof(ConsolidatedActDialog))]
    [InlineData(typeof(CustomersView))]
    [InlineData(typeof(BusinessSettingsView))]
    [InlineData(typeof(TransactionDetailDialog))]
    [InlineData(typeof(ReturnsView))]
    [InlineData(typeof(SalesView))]
    public void Every_screen_loads_without_a_binding_error(Type viewType)
    {
        using var listener = new BindingErrorListener();

        var view = (Control)Activator.CreateInstance(viewType)!;
        var window = new Window { Content = view, Width = 1280, Height = 800 };
        window.Show();
        // A second pass renders templates that only materialise once measured and arranged.
        window.Measure(new Size(1280, 800));
        window.Arrange(new Rect(0, 0, 1280, 800));
        window.Close();

        Assert.True(listener.Errors.Count == 0,
            $"{viewType.Name} reported binding errors:{Environment.NewLine}  "
            + string.Join(Environment.NewLine + "  ", listener.Errors.Distinct()));
    }

    /// Avalonia reports binding failures through the trace system rather than by throwing, so the
    /// only way to fail a test on them is to listen for them.
    private sealed class BindingErrorListener : TraceListener
    {
        public List<string> Errors { get; } = [];

        public BindingErrorListener() => Trace.Listeners.Add(this);

        public override void Write(string? message) => Capture(message);
        public override void WriteLine(string? message) => Capture(message);

        private void Capture(string? message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            if (message.Contains("Error in binding", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Unable to resolve", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Could not find", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Static resource", StringComparison.OrdinalIgnoreCase))
                Errors.Add(message.Trim());
        }

        protected override void Dispose(bool disposing)
        {
            Trace.Listeners.Remove(this);
            base.Dispose(disposing);
        }
    }
}
