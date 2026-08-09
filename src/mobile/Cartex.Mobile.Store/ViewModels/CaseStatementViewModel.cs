using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.TradeCases;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace Cartex.Mobile.Store.ViewModels;

public partial class CaseStatementViewModel(ITradeCasesApi tradeCasesApi) : ObservableObject, IQueryAttributable
{
    public ObservableCollection<TradeCaseStatementEntryDto> Timeline { get; } = [];
    public ObservableCollection<TradeCaseStatementProductDto> Products { get; } = [];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isLoaded;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _caseNumber = "";
    [ObservableProperty] private string _customerName = "";
    [ObservableProperty] private string _currency = "";
    [ObservableProperty] private string _openingText = "";
    [ObservableProperty] private string _closingText = "";
    [ObservableProperty] private string _mode = "both";
    [ObservableProperty] private bool _useDateFilter;
    [ObservableProperty] private DateTime _fromDate = DateTime.Today.AddMonths(-1);
    [ObservableProperty] private DateTime _toDate = DateTime.Today;

    public bool ShowTimeline => Mode is "both" or "timeline";
    public bool ShowProducts => Mode is "both" or "consolidated";
    public bool HasTimeline => Timeline.Count > 0;
    public bool HasProducts => Products.Count > 0;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    private long _caseId;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value)) long.TryParse(value.ToString(), out _caseId);
    }

    public async Task AppearAsync()
    {
        if (!IsLoaded && !IsLoading) await LoadAsync();
    }

    partial void OnModeChanged(string value)
    {
        OnPropertyChanged(nameof(ShowTimeline));
        OnPropertyChanged(nameof(ShowProducts));
    }

    [RelayCommand]
    private void SelectMode(string value) => Mode = value;

    [RelayCommand]
    private async Task ApplyFilterAsync() => await LoadAsync();

    [RelayCommand]
    private async Task ExportAsync(string format)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var from = UseDateFilter ? FromDate.Date : (DateTime?)null;
            DateTime? to = UseDateFilter ? ToDate.Date.AddDays(1) : null;
            using var content = await tradeCasesApi.ExportStatementAsync(_caseId, format, Mode, from, to);
            var bytes = await content.ReadAsByteArrayAsync();
            var safe = string.Concat(CaseNumber.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_'));
            var path = Path.Combine(FileSystem.CacheDirectory, $"{safe}-{Mode}.{format}");
            await File.WriteAllBytesAsync(path, bytes);
            await Share.Default.RequestAsync(new ShareFileRequest(Loc.Instance["statement"], new ShareFile(path)));
        }
        catch (Exception ex) { Error = Describe(ex); }
        finally { IsBusy = false; Notify(); }
    }

    private async Task LoadAsync()
    {
        if (_caseId <= 0 || IsLoading) return;
        IsLoading = true;
        Error = null;
        try
        {
            var from = UseDateFilter ? FromDate.Date : (DateTime?)null;
            DateTime? to = UseDateFilter ? ToDate.Date.AddDays(1) : null;
            var statement = await tradeCasesApi.StatementAsync(_caseId, from, to);
            Title = statement.CaseTitle;
            CaseNumber = statement.CaseNumber;
            CustomerName = statement.CustomerName;
            Currency = statement.Currency;
            OpeningText = $"{statement.OpeningBalance:N0} {statement.Currency}";
            ClosingText = $"{statement.ClosingBalance:N0} {statement.Currency}";
            Replace(Timeline, statement.Timeline);
            Replace(Products, statement.Products);
            IsLoaded = true;
        }
        catch (Exception ex) { Error = Describe(ex); }
        finally { IsLoading = false; Notify(); }
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values) target.Add(value);
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(HasTimeline));
        OnPropertyChanged(nameof(HasProducts));
        OnPropertyChanged(nameof(HasError));
    }

    private static string Describe(Exception exception) => exception is ApiException api
        ? ApiErrors.Describe(api)
        : Loc.Instance["err_no_connection"];
}
