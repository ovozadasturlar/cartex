using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Sales;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace Cartex.UI.ViewModels;

public partial class ReportsViewModel(ISalesApi salesApi) : ViewModelBase
{
    [ObservableProperty] private DateTimeOffset _dateFrom = DateTimeOffset.Now.AddDays(-30);
    [ObservableProperty] private DateTimeOffset _dateTo = DateTimeOffset.Now;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private decimal _totalSales;
    [ObservableProperty] private int _totalTransactions;
    [ObservableProperty] private decimal _averageSale;
    [ObservableProperty] private decimal _maxSale;
    [ObservableProperty] private List<SaleDto> _sales = [];

    public ObservableCollection<ISeries> SalesChartSeries { get; } = [];
    [ObservableProperty] private Axis[] _salesXAxes = [new Axis()];
    [ObservableProperty] private Axis[] _salesYAxes = [new Axis()];

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var from = DateFrom.DateTime;
            var to = DateTo.DateTime.Date.AddDays(1);
            var result = await salesApi.GetAllAsync(fromDate: from, toDate: to);
            Sales = result;
            TotalSales = result.Sum(s => s.TotalAmount);
            TotalTransactions = result.Count;
            AverageSale = TotalTransactions > 0 ? TotalSales / TotalTransactions : 0;
            MaxSale = result.MaxBy(s => s.TotalAmount)?.TotalAmount ?? 0;

            try
            {
                var grouped = result.GroupBy(s => s.SaleDate.Date)
                    .OrderBy(g => g.Key)
                    .ToList();

                SalesChartSeries.Clear();
                SalesChartSeries.Add(new ColumnSeries<decimal>
                {
                    Values = grouped.Select(g => g.Sum(s => s.TotalAmount)).ToArray(),
                    Fill = new SolidColorPaint(SKColor.Parse("#166534")),
                    MaxBarWidth = 32,
                    Rx = 4,
                    Ry = 4
                });

                SalesXAxes = [new Axis
                {
                    Labels = grouped.Select(g => g.Key.ToString("dd/MM")).ToArray(),
                    LabelsPaint = new SolidColorPaint(SKColor.Parse("#94A3B8")),
                    TextSize = 12
                }];

                SalesYAxes = [new Axis
                {
                    LabelsPaint = new SolidColorPaint(SKColor.Parse("#94A3B8")),
                    TextSize = 12
                }];
            }
            catch
            {
                SalesChartSeries.Clear();
                SalesXAxes = [new Axis()];
                SalesYAxes = [new Axis()];
            }
        }
        catch
        {
            Sales = [];
            TotalSales = 0;
            TotalTransactions = 0;
            AverageSale = 0;
            MaxSale = 0;
            SalesChartSeries.Clear();
            SalesXAxes = [new Axis()];
            SalesYAxes = [new Axis()];
        }
        finally
        {
            IsLoading = false;
        }
    }
}
