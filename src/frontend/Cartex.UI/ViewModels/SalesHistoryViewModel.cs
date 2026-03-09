using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Sales;

namespace Cartex.UI.ViewModels;

public partial class SalesHistoryViewModel : ViewModelBase
{
    private readonly ISalesApi _salesApi;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private DateTimeOffset? _dateFrom;
    [ObservableProperty] private DateTimeOffset? _dateTo;

    public ObservableCollection<SaleDto> Sales { get; } = [];

    public SalesHistoryViewModel(ISalesApi salesApi)
    {
        _salesApi = salesApi;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var from = DateFrom?.DateTime;
            var to = DateTo?.DateTime;
            var sales = await _salesApi.GetAllAsync(fromDate: from, toDate: to);
            Sales.Clear();
            foreach (var s in sales)
                Sales.Add(s);
        }
        catch { }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();
}
