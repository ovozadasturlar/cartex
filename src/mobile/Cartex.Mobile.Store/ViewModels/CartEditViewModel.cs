using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Querying;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.Ordering;
using Cartex.Shared.Models.Partners;
using Cartex.Shared.Models.Products;
using Cartex.Shared.Models.Rates;
using Cartex.Shared.Models.Sales;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace Cartex.Mobile.Store.ViewModels;

public partial class CartEditViewModel(
    IOrderingApi orderingApi,
    IProductsApi productsApi,
    ICustomersApi customersApi,
    IRatesApi ratesApi) : ObservableObject, IQueryAttributable
{
    public ObservableCollection<CartEditLine> Lines { get; } = [];
    public ObservableCollection<CartEditSearchRow> SearchResults { get; } = [];
    public ObservableCollection<CustomerDto> Customers { get; } = [];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isLoaded;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private string _customerSearch = "";
    [ObservableProperty] private string _customerName = "";
    [ObservableProperty] private string _note = "";
    [ObservableProperty] private string _totalText = "";

    public bool HasCustomer => _customerId.HasValue;
    public bool HasSearchResults => SearchResults.Count > 0;
    public bool HasCustomers => Customers.Count > 0;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public bool IsEmpty => Lines.Count == 0;

    private string _code = "";
    private CartDto? _cart;
    private long? _customerId;
    private CancellationTokenSource? _productSearchCts;
    private CancellationTokenSource? _customerSearchCts;
    private IReadOnlyList<CurrencyDto>? _currencies;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("code", out var value))
            _code = value.ToString() ?? "";
    }

    public async Task AppearAsync()
    {
        if (!IsLoaded && !IsLoading)
            await LoadAsync();
    }

    partial void OnSearchTextChanged(string value) => DebounceProductSearch(value);
    partial void OnCustomerSearchChanged(string value) => DebounceCustomerSearch(value);

    [RelayCommand]
    private void Increment(CartEditLine line)
    {
        line.Quantity += 1m;
        Recalculate();
    }

    [RelayCommand]
    private void Decrement(CartEditLine line)
    {
        var next = Math.Max(1m, line.Quantity - 1m);
        if (next < line.Quantity)
            line.Quantity = next;
        Recalculate();
    }

    [RelayCommand]
    private void CommitQuantity(CartEditLine line)
    {
        if (QuantityInput.TryParse(line.QuantityText, line.AllowsFractional,
                out var quantity, out var error))
        {
            line.Quantity = quantity;
            Recalculate();
            return;
        }

        line.QuantityText = QuantityInput.Format(line.Quantity);
        Ui.Toast(Loc.Instance[error switch
        {
            QuantityInputError.MustBePositive => "quantity_positive_required",
            QuantityInputError.FractionNotAllowed => "quantity_integer_required",
            _ => "quantity_invalid"
        }]);
    }

    [RelayCommand]
    private void Remove(CartEditLine line)
    {
        Lines.Remove(line);
        Recalculate();
    }

    [RelayCommand]
    private void PickProduct(CartEditSearchRow row)
    {
        var product = row.Product;
        var existing = Lines.FirstOrDefault(x => x.VariantId == product.DefaultVariantId);
        if (existing is null)
        {
            Lines.Add(new CartEditLine(
                product.DefaultVariantId,
                product.Name,
                product.UnitName,
                1m,
                row.BasePrice,
                product.AllowsFractional,
                product.ImageKey));
        }
        else
        {
            existing.Quantity += 1m;
        }

        SearchText = "";
        SearchResults.Clear();
        NotifyCollections();
        Recalculate();
    }

    [RelayCommand]
    private void PickCustomer(CustomerDto customer)
    {
        _customerId = customer.Id;
        CustomerName = customer.FullName;
        CustomerSearch = "";
        Customers.Clear();
        NotifyCollections();
    }

    [RelayCommand]
    private void ClearCustomer()
    {
        _customerId = null;
        CustomerName = "";
        NotifyCollections();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_cart is null || Lines.Count == 0 || IsBusy) return;
        foreach (var line in Lines)
            CommitQuantity(line);

        IsBusy = true;
        Error = null;
        try
        {
            await orderingApi.UpdateAsync(_code, new UpdateCartRequest(
                _customerId,
                Lines.Select(x => new SubmitCartItemRequest(x.VariantId, x.Quantity, x.PriceOverride)).ToList())
            {
                Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(),
                Participants = _cart.Participants?.Select(x => new ParticipantSelectionRequest(x.RoleDefinitionId, x.PartyId)).ToList(),
                Payments = _cart.Payments?.Select(x => new SalePaymentRequest(x.Method, x.Currency, x.Amount)).ToList(),
                DebtCurrency = _cart.DebtCurrency,
                DebtDueDate = _cart.DebtDueDate,
                CreditAmount = _cart.CreditAmount,
                UseCustomerAdvance = _cart.UseCustomerAdvance,
                ExpectedVersion = _cart.Version
            });
            Ui.Toast(Loc.Instance["saved_successfully"]);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            Error = Describe(ex);
            NotifyCollections();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(_code)) return;
        IsLoading = true;
        Error = null;
        try
        {
            var cart = _cart = await orderingApi.GetByCodeAsync(_code);
            if (cart.AllowedActions?.Contains("edit") != true)
                throw new InvalidOperationException(Loc.Instance["cart_not_editable"]);

            _customerId = cart.CustomerId;
            CustomerName = cart.CustomerName ?? "";
            Note = cart.Note ?? "";
            Lines.Clear();
            foreach (var item in cart.Items)
            {
                Lines.Add(new CartEditLine(item.VariantId, item.ProductName, item.UnitName,
                    item.Quantity, item.UnitPrice, item.AllowsFractional, item.ImageKey,
                    item.OriginalUnitPrice));
            }
            Recalculate();
            IsLoaded = true;
        }
        catch (Exception ex)
        {
            Error = Describe(ex);
        }
        finally
        {
            IsLoading = false;
            NotifyCollections();
        }
    }

    private void DebounceProductSearch(string value)
    {
        var owner = Debounce.Restart(ref _productSearchCts);
        _ = SearchProductsAsync(value, owner);
    }

    private async Task SearchProductsAsync(string value, CancellationTokenSource owner)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            SearchResults.Clear();
            NotifyCollections();
            return;
        }

        try
        {
            await Task.Delay(250, owner.Token);
            IsSearching = true;
            var products = (await productsApi.QueryAsync(
                QueryRequest.Create().Page(1, 20).Search(value.Trim()).Build())).Content ?? [];
            _currencies ??= await ratesApi.GetCurrenciesAsync(onlyEnabled: true);
            if (owner.IsCancellationRequested) return;

            var baseCurrency = _currencies.FirstOrDefault(x => x.IsBase)?.Code ?? "UZS";
            SearchResults.Clear();
            foreach (var product in products.Where(x => x.IsEnabled))
            {
                var currency = product.PriceCurrency ?? baseCurrency;
                var rate = string.Equals(currency, baseCurrency, StringComparison.OrdinalIgnoreCase)
                    ? 1m
                    : _currencies.FirstOrDefault(x => string.Equals(x.Code, currency, StringComparison.OrdinalIgnoreCase))?.Rate ?? 0;
                if (rate <= 0) continue;
                SearchResults.Add(new CartEditSearchRow(product,
                    Math.Round((product.SellingPrice ?? 0) * rate, 2), baseCurrency));
            }
            NotifyCollections();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!owner.IsCancellationRequested)
                Error = Describe(ex);
        }
        finally
        {
            if (ReferenceEquals(_productSearchCts, owner))
                IsSearching = false;
        }
    }

    private void DebounceCustomerSearch(string value)
    {
        var owner = Debounce.Restart(ref _customerSearchCts);
        _ = SearchCustomersAsync(value, owner);
    }

    private async Task SearchCustomersAsync(string value, CancellationTokenSource owner)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Customers.Clear();
            NotifyCollections();
            return;
        }

        try
        {
            await Task.Delay(250, owner.Token);
            var result = (await customersApi.QueryAsync(
                QueryRequest.Create().Page(1, 10).Search(value.Trim()).Build())).Content ?? [];
            if (owner.IsCancellationRequested) return;
            Customers.Clear();
            foreach (var customer in result)
                Customers.Add(customer);
            NotifyCollections();
        }
        catch (OperationCanceledException) { }
        catch { }
    }

    private void Recalculate()
    {
        TotalText = $"{Lines.Sum(x => x.LineTotal):N0} UZS";
        NotifyCollections();
    }

    private void NotifyCollections()
    {
        OnPropertyChanged(nameof(HasCustomer));
        OnPropertyChanged(nameof(HasSearchResults));
        OnPropertyChanged(nameof(HasCustomers));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(IsEmpty));
    }

    private static string Describe(Exception exception) => exception switch
    {
        ApiException api => ApiErrors.Describe(api),
        InvalidOperationException invalid => invalid.Message,
        _ => Loc.Instance["err_no_connection"]
    };
}

public sealed partial class CartEditLine : ObservableObject
{
    public long VariantId { get; }
    public string ProductName { get; }
    public string UnitName { get; }
    public decimal UnitPrice { get; }
    public decimal OriginalUnitPrice { get; }
    public decimal? PriceOverride => UnitPrice != OriginalUnitPrice ? UnitPrice : null;
    public bool AllowsFractional { get; }
    public string? ImageKey { get; }
    public decimal LineTotal => UnitPrice * Quantity;

    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private string _quantityText;

    public CartEditLine(long variantId, string productName, string unitName, decimal quantity,
        decimal unitPrice, bool allowsFractional, string? imageKey, decimal? originalUnitPrice = null)
    {
        VariantId = variantId;
        ProductName = productName;
        UnitName = unitName;
        UnitPrice = unitPrice;
        OriginalUnitPrice = originalUnitPrice ?? unitPrice;
        AllowsFractional = allowsFractional;
        ImageKey = imageKey;
        _quantity = quantity;
        _quantityText = QuantityInput.Format(quantity);
    }

    partial void OnQuantityChanged(decimal value)
    {
        QuantityText = QuantityInput.Format(value);
        OnPropertyChanged(nameof(LineTotal));
    }
}

public sealed record CartEditSearchRow(ProductDto Product, decimal BasePrice, string BaseCurrency)
{
    public string Name => Product.Name;
    public string Meta => $"{Product.UnitName} · {BasePrice:N0} {BaseCurrency}";
}
