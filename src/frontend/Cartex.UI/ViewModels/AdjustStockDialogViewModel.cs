using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;

namespace Cartex.UI.ViewModels;

public record AdjustStockResult(decimal CountedQuantity, string? Reason);

public partial class AdjustStockDialogViewModel : ViewModelBase, IDialogContext
{
    public string ProductName { get; }
    public string UnitName { get; }
    public decimal SystemQuantity { get; }

    [ObservableProperty] private decimal _countedQuantity;
    [ObservableProperty] private string _reason = string.Empty;

    public AdjustStockDialogViewModel(string productName, string unitName, decimal systemQuantity)
    {
        ProductName = productName;
        UnitName = unitName;
        SystemQuantity = systemQuantity;
        CountedQuantity = systemQuantity;
    }

    public decimal Difference => CountedQuantity - SystemQuantity;

    partial void OnCountedQuantityChanged(decimal value) => OnPropertyChanged(nameof(Difference));

    public event EventHandler<object?>? RequestClose;

    [RelayCommand]
    private void Confirm() =>
        RequestClose?.Invoke(this, new AdjustStockResult(CountedQuantity, string.IsNullOrWhiteSpace(Reason) ? null : Reason.Trim()));

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(this, null);

    public void Close() => RequestClose?.Invoke(this, null);
}
