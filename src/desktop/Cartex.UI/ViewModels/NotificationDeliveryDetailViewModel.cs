using Cartex.Shared.Models.Notifications;
using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;

namespace Cartex.UI.ViewModels;

public partial class NotificationDeliveryDetailViewModel(NotificationDeliveryDto delivery)
    : ViewModelBase, IDialogContext
{
    public NotificationDeliveryDto Delivery { get; } = delivery;

    public event EventHandler<object?>? RequestClose;

    [RelayCommand]
    private void CloseDialog() => RequestClose?.Invoke(this, null);

    public void Close() => RequestClose?.Invoke(this, null);
}
