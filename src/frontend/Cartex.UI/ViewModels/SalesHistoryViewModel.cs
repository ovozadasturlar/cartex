using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Sales;

namespace Cartex.UI.ViewModels;

public partial class SalesHistoryViewModel(ISalesApi salesApi) : ViewModelBase
{
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private DateTimeOffset? _dateFrom;
    [ObservableProperty] private DateTimeOffset? _dateTo;
    [ObservableProperty] private List<SaleDto> _sales = [];

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var result = await salesApi.GetAllAsync(
                fromDate: DateFrom?.DateTime,
                toDate: DateTo?.DateTime);
            Sales = result;
        }
        catch { Sales = []; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();
}
