using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Querying;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.Products;
using Cartex.Shared.Models.Rates;
using Cartex.Shared.Models.TradeCases;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace Cartex.Mobile.Store.ViewModels;

public partial class CaseIssueViewModel(
    ITradeCasesApi tradeCasesApi,
    IProductsApi productsApi,
    IRatesApi ratesApi) : ObservableObject, IQueryAttributable
{
    public ObservableCollection<CartEditLine> Lines { get; } = [];
    public ObservableCollection<CartEditSearchRow> SearchResults { get; } = [];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isLoaded;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string _caseTitle = "";
    [ObservableProperty] private string _customerName = "";
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _barcodeText = "";
    [ObservableProperty] private string _note = "";
    [ObservableProperty] private string _totalText = "0";

    public bool HasResults => SearchResults.Count > 0;
    public bool HasLines => Lines.Count > 0;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    private long _caseId;
    private TradeCaseDetailDto? _case;
    private CancellationTokenSource? _searchCts;
    private IReadOnlyList<CurrencyDto>? _currencies;
    private readonly string _idempotencyKey = Guid.NewGuid().ToString("N");

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value)) long.TryParse(value.ToString(), out _caseId);
    }

    public async Task AppearAsync()
    {
        if (IsLoaded || IsLoading || _caseId <= 0) return;
        IsLoading = true;
        try
        {
            _case = await tradeCasesApi.GetByIdAsync(_caseId);
            CaseTitle = $"{_case.CaseNumber} · {_case.Title}";
            CustomerName = _case.CustomerName;
            IsLoaded = true;
        }
        catch (Exception ex) { Error = Describe(ex); }
        finally { IsLoading = false; Notify(); }
    }

    partial void OnSearchTextChanged(string value)
    {
        _searchCts?.Cancel();
        var owner = _searchCts = new CancellationTokenSource();
        _ = SearchAsync(value, owner);
    }

    [RelayCommand]
    private async Task AddBarcodeAsync()
    {
        if (_case is null || string.IsNullOrWhiteSpace(BarcodeText) || IsBusy) return;
        IsBusy = true;
        try
        {
            var product = await productsApi.GetByBarcodeAsync(BarcodeText.Trim(), _case.WarehouseId, forSale: false);
            AddLookup(product);
            BarcodeText = "";
        }
        catch (Exception ex) { Error = Describe(ex); }
        finally { IsBusy = false; Notify(); }
    }

    [RelayCommand]
    private void PickProduct(CartEditSearchRow row)
    {
        var product = row.Product;
        AddLine(product.DefaultVariantId, product.Name, product.UnitName,
            product.QuantityStep, product.AllowsFractional, 1, row.BasePrice, product.ImageKey);
        SearchText = "";
        SearchResults.Clear();
        Recalculate();
    }

    [RelayCommand]
    private void Increment(CartEditLine line) { line.Quantity += line.IncrementStep; Recalculate(); }

    [RelayCommand]
    private void Decrement(CartEditLine line)
    {
        var next = Math.Max(line.QuantityStep, line.Quantity - line.IncrementStep);
        if (next < line.Quantity) line.Quantity = next;
        Recalculate();
    }

    [RelayCommand]
    private void CommitQuantity(CartEditLine line)
    {
        if (QuantityInput.TryParse(line.QuantityText, line.QuantityStep, line.AllowsFractional,
                out var quantity, out var error))
        {
            line.Quantity = quantity;
            Recalculate();
            return;
        }
        line.QuantityText = QuantityInput.Format(line.Quantity);
        Ui.Toast(Loc.Instance[error switch
        {
            QuantityInputError.FractionNotAllowed => "quantity_integer_required",
            QuantityInputError.StepMismatch => "quantity_step_invalid",
            QuantityInputError.MustBePositive => "quantity_positive_required",
            _ => "quantity_invalid"
        }]);
    }

    [RelayCommand]
    private void Remove(CartEditLine line) { Lines.Remove(line); Recalculate(); }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_case is null || Lines.Count == 0 || IsBusy) return;
        IsBusy = true;
        Error = null;
        try
        {
            var result = await tradeCasesApi.IssueAsync(_caseId, new CreateGoodsIssueRequest(
                Lines.Select(x => new GoodsIssueLineRequest(x.VariantId, x.Quantity)).ToList(),
                Note: string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(),
                IdempotencyKey: _idempotencyKey,
                ExpectedCaseVersion: _case.Version));
            Ui.Toast(string.Format(Loc.Instance["issue_created_fmt"], result.DocumentNumber));
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex) { Error = Describe(ex); }
        finally { IsBusy = false; Notify(); }
    }

    private void AddLookup(ProductLookupDto product) => AddLine(
        product.VariantId, product.ProductName, product.UnitName, product.QuantityStep,
        product.AllowsFractional, product.PackQty > 0 ? product.PackQty : 1,
        product.SellingPrice, product.ImageKey);

    private void AddLine(long variantId, string name, string unit, decimal stepValue,
        bool fractional, decimal initial, decimal price, string? imageKey)
    {
        var step = QuantityInput.NormalizeStep(stepValue, fractional);
        var requestedIncrement = initial > 0 ? initial : 1;
        var increment = requestedIncrement % step == 0
            ? requestedIncrement
            : 1m % step == 0 ? 1 : step;
        var firstQuantity = initial > 0 && initial % step == 0 ? initial : Math.Max(step, increment);
        var existing = Lines.FirstOrDefault(x => x.VariantId == variantId);
        if (existing is null)
            Lines.Add(new CartEditLine(variantId, name, unit, firstQuantity, price,
                step, increment, fractional, imageKey));
        else
            existing.Quantity += increment;
        Recalculate();
    }

    private async Task SearchAsync(string value, CancellationTokenSource owner)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            SearchResults.Clear(); Notify(); return;
        }
        try
        {
            await Task.Delay(250, owner.Token);
            IsSearching = true;
            var products = (await productsApi.QueryAsync(QueryRequest.Create().Page(1, 20).Search(value.Trim()).Build())).Content ?? [];
            _currencies ??= await ratesApi.GetCurrenciesAsync(onlyEnabled: true);
            if (owner.IsCancellationRequested) return;
            var baseCurrency = _currencies.FirstOrDefault(x => x.IsBase)?.Code ?? "UZS";
            SearchResults.Clear();
            foreach (var product in products.Where(x => x.IsEnabled))
            {
                var code = product.PriceCurrency ?? baseCurrency;
                var rate = string.Equals(code, baseCurrency, StringComparison.OrdinalIgnoreCase)
                    ? 1 : _currencies.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase))?.Rate ?? 0;
                if (rate > 0) SearchResults.Add(new CartEditSearchRow(product,
                    Math.Round((product.SellingPrice ?? 0) * rate, 2), baseCurrency));
            }
            Notify();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!owner.IsCancellationRequested) Error = Describe(ex); }
        finally { if (ReferenceEquals(_searchCts, owner)) IsSearching = false; }
    }

    private void Recalculate()
    {
        TotalText = $"{Lines.Sum(x => x.LineTotal):N0}";
        Notify();
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(HasLines));
        OnPropertyChanged(nameof(HasError));
    }

    private static string Describe(Exception exception) => exception is ApiException api
        ? ApiErrors.Describe(api)
        : Loc.Instance["err_no_connection"];
}
