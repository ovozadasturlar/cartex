using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Sales;

namespace Cartex.UI.ViewModels;

public partial class ReportsViewModel(ISalesApi salesApi) : ViewModelBase
{
    [ObservableProperty] private DateTimeOffset _dateFrom = DateTimeOffset.Now.AddDays(-30);
    [ObservableProperty] private DateTimeOffset _dateTo = DateTimeOffset.Now;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private decimal _totalSales;
    [ObservableProperty] private int _totalTransactions;

    public ObservableCollection<SaleDto> Sales { get; } = [];

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var from = DateFrom.DateTime;
            var to = DateTo.DateTime.Date.AddDays(1);
            var sales = await salesApi.GetAllAsync(fromDate: from, toDate: to);
            Sales.Clear();
            foreach (var s in sales)
                Sales.Add(s);
            TotalSales = sales.Sum(s => s.TotalAmount);
            TotalTransactions = sales.Count;
        }
        catch
        {
            Sales.Clear();
            TotalSales = 0;
            TotalTransactions = 0;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
