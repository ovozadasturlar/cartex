using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Interactivity;
using Cartex.UI.Services;

namespace Cartex.UI.Views;

public partial class DesktopShell : UserControl
{
    public DesktopShell()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        var top = TopLevel.GetTopLevel(this);
        if (top is not null)
        {
            var manager = new WindowNotificationManager(top)
            {
                Position = NotificationPosition.TopRight,
                MaxItems = 4
            };
            ServiceLocator.Resolve<ToastService>().Attach(manager);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.K && e.KeyModifiers == KeyModifiers.Control)
        {
            this.FindControl<TextBox>("SearchBox")?.Focus();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }
}
