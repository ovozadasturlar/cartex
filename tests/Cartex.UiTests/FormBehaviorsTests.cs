using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Cartex.UI.Controls;
using Xunit;

namespace Cartex.UiTests;

public class FormBehaviorsTests
{
    [AvaloniaFact]
    public void Enter_on_container_moves_between_fields_and_submits_last()
    {
        var submitted = false;
        var first = new TextBox();
        var second = new TextBox();
        var form = new StackPanel { Children = { first, second } };
        FormBehaviors.SetEnterMovesNext(form, true);
        FormBehaviors.SetEnterSubmits(form, new TestCommand(() => submitted = true));

        var window = new Window { Content = form };
        window.Show();
        first.Focus();

        first.RaiseEvent(EnterFrom(first));
        Assert.Same(second, window.FocusManager?.GetFocusedElement());

        second.RaiseEvent(EnterFrom(second));
        Assert.True(submitted);
        window.Close();
    }

    private static KeyEventArgs EnterFrom(Control source) => new()
    {
        RoutedEvent = InputElement.KeyDownEvent,
        Key = Key.Enter,
        Source = source
    };

    private sealed class TestCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute();
    }
}
