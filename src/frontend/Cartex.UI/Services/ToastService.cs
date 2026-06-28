using Avalonia.Controls.Notifications;
using Avalonia.Threading;

namespace Cartex.UI.Services;

public interface IToastService
{
    void Success(string message, string? title = null);
    void Error(string message, string? title = null);
    void Info(string message, string? title = null);
    void Warning(string message, string? title = null);
}

public sealed class ToastService : IToastService
{
    private WindowNotificationManager? _manager;

    public void Attach(WindowNotificationManager manager) => _manager = manager;

    public void Success(string message, string? title = null) => Show(message, title, NotificationType.Success);
    public void Error(string message, string? title = null) => Show(message, title, NotificationType.Error);
    public void Info(string message, string? title = null) => Show(message, title, NotificationType.Information);
    public void Warning(string message, string? title = null) => Show(message, title, NotificationType.Warning);

    private void Show(string message, string? title, NotificationType type)
    {
        if (_manager is null) return;
        Dispatcher.UIThread.Post(() => _manager.Show(new Notification(title, message, type)));
    }
}
