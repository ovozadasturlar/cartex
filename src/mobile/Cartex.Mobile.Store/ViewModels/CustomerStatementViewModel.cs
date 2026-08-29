using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.Customers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace Cartex.Mobile.Store.ViewModels;

public partial class CustomerStatementViewModel(
    ICustomersApi customersApi,
    AccessState access) : AccessAwareViewModel(access), IQueryAttributable
{
    public ObservableCollection<CustomerStatementBalanceDto> Balances { get; } = [];
    public ObservableCollection<CustomerStatementDisplayRow> Rows { get; } = [];
    public IReadOnlyList<CustomerStatementScopeChoice> Scopes { get; } =
    [
        new(null, Loc.Instance["all_documents"]),
        new("Sale,CustomerReturn", Loc.Instance["sales_and_returns"]),
        new("CustomerPayment,CustomerRefund", Loc.Instance["payments_and_refunds"])
    ];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isLoaded;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string _customerName = "";
    [ObservableProperty] private string _baseCurrency = "";
    [ObservableProperty] private string _mode = "both";
    [ObservableProperty] private bool _useDateFilter;
    [ObservableProperty] private DateTime _fromDate = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime _toDate = DateTime.Today;
    [ObservableProperty] private CustomerStatementScopeChoice? _selectedScope;
    public bool CanExport => Access.CanExportCustomerStatement;

    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    private long _customerId;
    private CustomerStatementDto? _statement;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value)) long.TryParse(value.ToString(), out _customerId);
    }

    public async Task AppearAsync()
    {
        if (IsLoaded || IsLoading || _customerId <= 0) return;
        ObserveAccess(nameof(CanExport));
        SelectedScope = Scopes[0];
        IsLoading = true;
        Error = null;
        try
        {
            Apply(await GetStatementAsync());
            IsLoaded = true;
        }
        catch (Exception ex) { Error = Describe(ex); }
        finally { IsLoading = false; Notify(); }
    }

    partial void OnModeChanged(string value) => RebuildRows();

    [RelayCommand]
    private void SelectMode(string value) => Mode = value;

    [RelayCommand]
    private async Task ApplyFilterAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        Error = null;
        try { Apply(await GetStatementAsync()); }
        catch (Exception ex) { Error = Describe(ex); }
        finally { IsLoading = false; Notify(); }
    }

    [RelayCommand]
    private async Task ExportAsync(string format)
    {
        if (!CanExport || IsBusy) return;
        IsBusy = true;
        Error = null;
        try
        {
            var (from, to) = Range();
            using var content = await customersApi.ExportStatementAsync(
                _customerId, format, Mode, from, to, documentTypes: SelectedScope?.DocumentTypes);
            var bytes = await content.ReadAsByteArrayAsync();
            var safe = string.Concat(CustomerName.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_'));
            var path = Path.Combine(FileSystem.CacheDirectory, $"{safe}-{Mode}.{format}");
            await File.WriteAllBytesAsync(path, bytes);
            await Share.Default.RequestAsync(new ShareFileRequest(Loc.Instance["statement"], new ShareFile(path)));
        }
        catch (Exception ex) { Error = Describe(ex); }
        finally { IsBusy = false; Notify(); }
    }

    [RelayCommand]
    private Task OpenRowAsync(CustomerStatementDisplayRow row)
    {
        if (row.Entry?.SaleId is long saleId)
            return Shell.Current.GoToAsync($"sale/detail?id={saleId}");
        return Task.CompletedTask;
    }

    private Task<CustomerStatementDto> GetStatementAsync()
    {
        var (from, to) = Range();
        return customersApi.GetStatementAsync(
            _customerId, from, to, documentTypes: SelectedScope?.DocumentTypes);
    }

    private (DateTime? From, DateTime? To) Range() => UseDateFilter
        ? (FromDate.Date, ToDate.Date.AddDays(1))
        : (null, null);

    private void Apply(CustomerStatementDto value)
    {
        _statement = value;
        CustomerName = value.CustomerName;
        BaseCurrency = value.BaseCurrency;
        Replace(Balances, value.Balances);
        RebuildRows();
    }

    private void RebuildRows()
    {
        Rows.Clear();
        if (_statement is null) return;
        if (Mode is "both" or "timeline")
        {
            Rows.Add(CustomerStatementDisplayRow.Header(Loc.Instance["statement_timeline"]));
            foreach (var entry in _statement.Timeline)
                Rows.Add(CustomerStatementDisplayRow.ForEntry(entry));
        }
        if (Mode is "both" or "consolidated")
        {
            Rows.Add(CustomerStatementDisplayRow.Header(Loc.Instance["statement_consolidated"]));
            foreach (var product in _statement.Products)
                Rows.Add(CustomerStatementDisplayRow.ForProduct(product, _statement.BaseCurrency));
        }
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values) target.Add(value);
    }

    private void Notify() => OnPropertyChanged(nameof(HasError));
    private static string Describe(Exception exception) => exception is ApiException api
        ? ApiErrors.Describe(api)
        : Loc.Instance["err_no_connection"];
}

public sealed record CustomerStatementScopeChoice(string? DocumentTypes, string Label);

public sealed class CustomerStatementDisplayRow
{
    public string Kind { get; private init; } = "header";
    public string Title { get; private init; } = "";
    public CustomerStatementEntryDto? Entry { get; private init; }
    public CustomerStatementProductDto? Product { get; private init; }
    public bool IsHeader => Kind == "header";
    public bool IsTimeline => Kind == "timeline";
    public bool IsProduct => Kind == "product";
    public string DebitText => Entry?.Debit > 0 ? $"+{Entry.Debit:N2} {Entry.Currency}" : "";
    public string CreditText => Entry?.Credit > 0 ? $"−{Entry.Credit:N2} {Entry.Currency}" : "";
    public string BalanceText => Entry is null ? "" : $"{Entry.RunningBalance:N2} {Entry.Currency}";
    public string ProductAmount { get; private init; } = "";

    public static CustomerStatementDisplayRow Header(string title) => new() { Title = title };
    public static CustomerStatementDisplayRow ForEntry(CustomerStatementEntryDto entry) =>
        new() { Kind = "timeline", Entry = entry };
    public static CustomerStatementDisplayRow ForProduct(CustomerStatementProductDto product, string currency) =>
        new() { Kind = "product", Product = product, ProductAmount = $"{product.ChargedBaseAmount:N2} {currency}" };
}
