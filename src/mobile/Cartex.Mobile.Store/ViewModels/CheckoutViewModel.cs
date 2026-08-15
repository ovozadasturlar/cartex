using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.Ordering;
using Cartex.Shared.Models.Partners;
using Cartex.Shared.Models.Rates;
using Cartex.Shared.Models.Sales;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace Cartex.Mobile.Store.ViewModels;

public partial class CheckoutViewModel : ObservableObject, IQueryAttributable
{
    private readonly IOrderingApi _orderingApi;
    private readonly IBusinessApi _businessApi;
    private readonly IRatesApi _ratesApi;
    private readonly CartStore _localCart;
    private readonly WarehouseContext _warehouse;
    private readonly MobilePermissions _permissions;
    private readonly MobileOfflineService _offline;

    public ObservableCollection<CheckoutLine> Items { get; } = [];
    public ObservableCollection<CheckoutParticipantLine> Participants { get; } = [];
    public ObservableCollection<CurrencyDto> Currencies { get; } = [];
    public ObservableCollection<CheckoutPaymentRow> Payments { get; } = [];
    public IReadOnlyList<CheckoutPaymentMethod> PaymentMethods { get; } =
    [
        new("Cash", Loc.Instance["pay_cash"]),
        new("Card", Loc.Instance["pay_card"]),
        new("Transfer", Loc.Instance["transfer"]),
        new("Bank", Loc.Instance["bank"]),
        new("Bonus", Loc.Instance["bonus"])
    ];

    [ObservableProperty] private bool _isLoaded;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string _customerName = "";
    [ObservableProperty] private bool _hasCustomer;
    [ObservableProperty] private string _noteText = "";
    [ObservableProperty] private bool _hasNote;
    [ObservableProperty] private string _totalText = "";
    [ObservableProperty] private string _cashText = "";
    [ObservableProperty] private string _cardText = "";
    [ObservableProperty] private string _bonusText = "";
    [ObservableProperty] private string _paidText = "";
    [ObservableProperty] private string _changeText = "";
    [ObservableProperty] private string _debtText = "";
    [ObservableProperty] private bool _canSelfSell;
    [ObservableProperty] private bool _canQueue = true;
    [ObservableProperty] private bool _canEditNote = true;
    [ObservableProperty] private bool _isMulticurrency;
    [ObservableProperty] private string _baseCurrency = "UZS";
    [ObservableProperty] private CurrencyDto? _selectedDebtCurrency;
    [ObservableProperty] private bool _useCustomerAdvance = true;
    [ObservableProperty] private bool _hasPaymentRateError;
    [ObservableProperty] private bool _hasRateWarning;
    [ObservableProperty] private bool _keepExcessAsCredit;
    [ObservableProperty] private bool _hasInvalidChange;
    [ObservableProperty] private string _excessText = "";

    public bool IsSimplePayment => !IsMulticurrency;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public bool CanAddPayment => Payments.Count < 20;
    public bool HasParticipants => Participants.Count > 0;
    public bool CanStoreExcessAsCredit => HasCustomer && Paid > _totalAmount;

    private string _code = "";
    private CartDto? _serverCart;
    private decimal _totalAmount;
    private long? _customerId;
    private bool _initializingPayments;

    public CheckoutViewModel(
        IOrderingApi orderingApi,
        IBusinessApi businessApi,
        IRatesApi ratesApi,
        CartStore localCart,
        WarehouseContext warehouse,
        MobilePermissions permissions,
        MobileOfflineService offline)
    {
        _orderingApi = orderingApi;
        _businessApi = businessApi;
        _ratesApi = ratesApi;
        _localCart = localCart;
        _warehouse = warehouse;
        _permissions = permissions;
        _offline = offline;
        CanSelfSell = permissions.Has("sales.checkout");
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("code", out var code))
            _code = code.ToString() ?? "";
    }

    public async Task AppearAsync()
    {
        if (IsLoaded) return;
        Error = null;

        await _offline.StartAsync();
        if (_offline.ShouldUseOffline)
        {
            if (!string.IsNullOrEmpty(_code))
            {
                await Shell.Current.CurrentPage.DisplayAlertAsync(
                    Loc.Instance["checkout"], Loc.Instance["offline_queue_requires_internet"], Loc.Instance["ok"]);
                await Shell.Current.GoToAsync("..");
                return;
            }
            IsMulticurrency = false;
            BaseCurrency = await _offline.BaseCurrencyAsync();
            Currencies.Clear();
            Currencies.Add(new CurrencyDto(BaseCurrency, BaseCurrency, true, true, true, true, 1, DateTime.UtcNow));
            ApplyLocalCart();
            InitializePayments();
            IsLoaded = true;
            Recalc();
            return;
        }

        var businessTask = _businessApi.GetAsync();
        var currenciesTask = _ratesApi.GetCurrenciesAsync(onlyEnabled: true);
        Task<CartDto?> cartTask = string.IsNullOrEmpty(_code)
            ? Task.FromResult<CartDto?>(null)
            : LoadServerCartAsync();

        if (!string.IsNullOrEmpty(_code))
        {
            try
            {
                _serverCart = await cartTask;
            }
            catch (Exception ex)
            {
                await Shell.Current.CurrentPage.DisplayAlertAsync(
                    Loc.Instance["checkout"], Describe(ex), Loc.Instance["ok"]);
                await Shell.Current.GoToAsync("..");
                return;
            }
        }

        try
        {
            await Task.WhenAll(businessTask, currenciesTask);
            var business = await businessTask;
            IsMulticurrency = business.SalesMulticurrency;
            BaseCurrency = business.Currency.ToUpperInvariant();
            ReplaceCurrencies(await currenciesTask);
        }
        catch
        {
            // Feature/bootstrap unavailable: safe base-currency fallback keeps a
            // local cart usable; server remains canonical on submission.
            _offline.MarkServerUnavailable();
            IsMulticurrency = false;
            BaseCurrency = _offline.IsEnabled ? await _offline.BaseCurrencyAsync() : "UZS";
            Currencies.Clear();
            Currencies.Add(new CurrencyDto(BaseCurrency, BaseCurrency, true, true, true, true, 1, DateTime.UtcNow));
        }

        if (_serverCart is not null)
            ApplyServerCart(_serverCart);
        else
            ApplyLocalCart();

        InitializePayments();
        IsLoaded = true;
        Recalc();
    }

    partial void OnCashTextChanged(string value) => Recalc();
    partial void OnCardTextChanged(string value) => Recalc();
    partial void OnBonusTextChanged(string value) => Recalc();
    partial void OnIsMulticurrencyChanged(bool value) => OnPropertyChanged(nameof(IsSimplePayment));
    partial void OnKeepExcessAsCreditChanged(bool value) => Recalc();

    [RelayCommand]
    private void Exact()
    {
        if (IsMulticurrency)
        {
            var row = Payments.FirstOrDefault(x => x.SelectedMethod?.Code == "Cash") ?? Payments.FirstOrDefault();
            if (row is not null) ExactPayment(row);
            return;
        }
        CashText = _totalAmount.ToString("0.##", CultureInfo.CurrentCulture);
    }

    [RelayCommand]
    private void AddPayment()
    {
        if (!CanAddPayment) return;
        AddPaymentRow("Cash", BaseCurrency, null);
        NotifyPaymentState();
    }

    [RelayCommand]
    private void RemovePayment(CheckoutPaymentRow row)
    {
        if (Payments.Count <= 1) return;
        row.PropertyChanged -= OnPaymentPropertyChanged;
        Payments.Remove(row);
        NotifyPaymentState();
        Recalc();
    }

    [RelayCommand]
    private void ExactPayment(CheckoutPaymentRow row)
    {
        var otherPaid = Payments.Where(x => !ReferenceEquals(x, row)).Sum(x => x.AmountBase);
        var remainingBase = Math.Max(0, _totalAmount - otherPaid);
        var rate = row.EffectiveRate;
        if (rate <= 0)
        {
            Ui.Toast(Loc.Instance["currency_rate_required"]);
            return;
        }
        var digits = Math.Clamp(row.SelectedCurrency?.DecimalDigits ?? 2, 0, 4);
        row.AmountText = Math.Round(remainingBase / rate, digits, MidpointRounding.AwayFromZero)
            .ToString($"F{digits}", CultureInfo.CurrentCulture);
    }

    [RelayCommand]
    private async Task QueueAsync()
    {
        if (IsBusy || !CanQueue || !ValidatePayments()) return;
        if (_offline.ShouldUseOffline)
        {
            Error = Loc.Instance["offline_queue_requires_internet"];
            NotifyError();
            return;
        }
        if (!await _warehouse.EnsureSelectedAsync())
        {
            Ui.Toast(Loc.Instance["warehouse_none"]);
            return;
        }

        IsBusy = true;
        Error = null;
        try
        {
            var aggregates = PaymentAggregates();
            var creditAmount = CreditAmount();
            var request = new SubmitCartRequest(
                _warehouse.WarehouseId!.Value,
                _customerId,
                _localCart.Lines.Select(l => new SubmitCartItemRequest(l.VariantId, l.Quantity)).ToList(),
                _localCart.EnsureSubmissionIdempotencyKey(),
                string.IsNullOrWhiteSpace(NoteText) ? null : NoteText.Trim(),
                PaidCash: aggregates.Cash,
                PaidCard: aggregates.Card,
                PaidBonus: aggregates.Bonus,
                Participants: BuildParticipantRequests(),
                Payments: BuildPaymentRequests(),
                DebtCurrency: SelectedDebtCurrency?.Code,
                CreditAmount: creditAmount,
                UseCustomerAdvance: UseCustomerAdvance);

            var code = await _orderingApi.SubmitAsync(request);
            _localCart.MarkSubmitted(code);
            _localCart.Clear();
            Ui.Toast(Loc.Instance["send_to_queue"]);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            if (IsConnectionFailure(ex)) _offline.MarkServerUnavailable();
            Error = Describe(ex);
        }
        finally { IsBusy = false; NotifyError(); }
    }

    [RelayCommand]
    private async Task CompleteAsync()
    {
        if (IsBusy || !ValidatePayments()) return;
        var paid = Paid;
        if (_totalAmount - paid > 0 && _customerId is null)
        {
            await Shell.Current.CurrentPage.DisplayAlertAsync(
                Loc.Instance["checkout"], Loc.Instance["err_debt_needs_customer"], Loc.Instance["ok"]);
            return;
        }

        IsBusy = true;
        Error = null;
        try
        {
            if (_offline.ShouldUseOffline)
            {
                await CompleteOfflineAsync();
                return;
            }
            var codeToCheckout = await EnsureCartCodeAsync();
            if (string.IsNullOrEmpty(codeToCheckout)) return;

            var aggregates = PaymentAggregates();
            var creditAmount = CreditAmount();
            await _orderingApi.CheckoutAsync(codeToCheckout, new CheckoutCartRequest(
                aggregates.Cash,
                aggregates.Card,
                aggregates.Bonus,
                CheckoutIdempotencyKey(),
                Payments: BuildPaymentRequests(),
                DebtCurrency: SelectedDebtCurrency?.Code,
                CreditAmount: creditAmount,
                UseCustomerAdvance: UseCustomerAdvance,
                CustomerId: _customerId,
                Note: string.IsNullOrWhiteSpace(NoteText) ? null : NoteText.Trim()));

            if (string.IsNullOrEmpty(_code))
                _localCart.Clear();
            Ui.Toast(Loc.Instance["sale_done"]);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            if (IsConnectionFailure(ex))
                _offline.MarkServerUnavailable();
            Error = Describe(ex);
        }
        finally
        {
            IsBusy = false;
            NotifyError();
        }
    }

    private async Task CompleteOfflineAsync()
    {
        if (!string.IsNullOrEmpty(_code))
            throw new InvalidOperationException(Loc.Instance["offline_queue_requires_internet"]);
        if (IsMulticurrency || PaymentAggregates().Bonus > 0)
            throw new InvalidOperationException(Loc.Instance["offline_base_currency_only"]);
        if (!await _warehouse.EnsureSelectedAsync() || _warehouse.WarehouseId is null)
            throw new InvalidOperationException(Loc.Instance["warehouse_none"]);

        var aggregates = PaymentAggregates();
        await _offline.EnqueueSaleAsync(new CreateSaleRequest(
            _warehouse.WarehouseId.Value,
            _customerId,
            aggregates.Cash,
            aggregates.Card,
            0,
            _localCart.Lines.Select(x => new CreateSaleItemRequest(
                x.VariantId, x.Quantity, x.UnitPrice)).ToList(),
            Payments: null,
            DebtCurrency: BaseCurrency,
            IdempotencyKey: CheckoutIdempotencyKey(),
            ApplyAutoDiscount: false,
            CreditAmount: CreditAmount(),
            UseCustomerAdvance: false,
            Participants: BuildParticipantRequests()));
        _localCart.Clear();
        Ui.Toast(Loc.Instance["offline_saved"]);
        await Shell.Current.GoToAsync("..");
    }

    private async Task<string> EnsureCartCodeAsync()
    {
        if (!string.IsNullOrEmpty(_code)) return _code;
        if (!await _warehouse.EnsureSelectedAsync())
        {
            Ui.Toast(Loc.Instance["warehouse_none"]);
            return "";
        }

        var existing = _localCart.SubmittedCartCode;
        if (!string.IsNullOrEmpty(existing)) return existing;

        var aggregates = PaymentAggregates();
        var creditAmount = CreditAmount();
        var code = await _orderingApi.SubmitAsync(new SubmitCartRequest(
            _warehouse.WarehouseId!.Value,
            _customerId,
            _localCart.Lines.Select(l => new SubmitCartItemRequest(l.VariantId, l.Quantity)).ToList(),
            _localCart.EnsureSubmissionIdempotencyKey(),
            string.IsNullOrWhiteSpace(NoteText) ? null : NoteText.Trim(),
            PaidCash: aggregates.Cash,
            PaidCard: aggregates.Card,
            PaidBonus: aggregates.Bonus,
            Participants: BuildParticipantRequests(),
            Payments: BuildPaymentRequests(),
            DebtCurrency: SelectedDebtCurrency?.Code,
            CreditAmount: creditAmount,
            UseCustomerAdvance: UseCustomerAdvance));
        _localCart.MarkSubmitted(code);
        return code;
    }

    private async Task<CartDto?> LoadServerCartAsync()
    {
        return await _orderingApi.GetByCodeAsync(_code);
    }

    private string CheckoutIdempotencyKey() => !string.IsNullOrEmpty(_code)
        ? $"cart-checkout:{_code}"
        : _localCart.EnsureCheckoutIdempotencyKey();

    private void ApplyServerCart(CartDto cart)
    {
        Items.Clear();
        Participants.Clear();
        foreach (var item in cart.Items)
            Items.Add(new CheckoutLine(item.ProductName, item.Quantity, item.UnitPrice, item.LineTotal));
        foreach (var participant in cart.Participants ?? [])
            Participants.Add(new CheckoutParticipantLine(participant.RoleLabel, participant.PartyName));
        _customerId = cart.CustomerId;
        CustomerName = cart.CustomerName ?? "";
        NoteText = cart.Note ?? "";
        _totalAmount = cart.Total;
        CashText = cart.PaidCash > 0 ? QuantityInput.Format(cart.PaidCash) : "";
        CardText = cart.PaidCard > 0 ? QuantityInput.Format(cart.PaidCard) : "";
        BonusText = cart.PaidBonus > 0 ? QuantityInput.Format(cart.PaidBonus) : "";
        KeepExcessAsCredit = cart.CreditAmount > 0;
        UseCustomerAdvance = cart.UseCustomerAdvance;
        CanQueue = false;
        CanEditNote = false;
        var actions = cart.AllowedActions ?? [];
        CanSelfSell = _permissions.Has("sales.checkout") &&
                      (actions.Contains("checkout") || actions.Contains("claim"));
        OnPropertyChanged(nameof(HasParticipants));
    }

    private void ApplyLocalCart()
    {
        Items.Clear();
        Participants.Clear();
        foreach (var line in _localCart.Lines)
            Items.Add(new CheckoutLine(line.ProductName, line.Quantity, line.UnitPrice, line.LineTotal));
        foreach (var participant in _localCart.Participants)
            Participants.Add(new CheckoutParticipantLine(participant.RoleLabel, participant.PartyName));
        _customerId = _localCart.CustomerId;
        CustomerName = _localCart.CustomerName ?? "";
        NoteText = _localCart.Note;
        _totalAmount = _localCart.Total;
        CanQueue = true;
        CanEditNote = true;
        OnPropertyChanged(nameof(HasParticipants));
    }

    private void InitializePayments()
    {
        _initializingPayments = true;
        foreach (var row in Payments)
            row.PropertyChanged -= OnPaymentPropertyChanged;
        Payments.Clear();

        if (IsMulticurrency)
        {
            if (_serverCart?.Payments is { Count: > 0 } existing)
            {
                foreach (var row in existing)
                    AddPaymentRow(row.Method, row.Currency, row.Amount);
            }
            else
            {
                if (Parse(CashText) > 0) AddPaymentRow("Cash", BaseCurrency, Parse(CashText));
                if (Parse(CardText) > 0) AddPaymentRow("Card", BaseCurrency, Parse(CardText));
                if (Parse(BonusText) > 0) AddPaymentRow("Bonus", BaseCurrency, Parse(BonusText));
                if (Payments.Count == 0) AddPaymentRow("Cash", BaseCurrency, null);
            }
        }

        SelectedDebtCurrency = !string.IsNullOrWhiteSpace(_serverCart?.DebtCurrency)
            ? FindCurrency(_serverCart!.DebtCurrency!)
            : FindCurrency(BaseCurrency);
        _initializingPayments = false;
        NotifyPaymentState();
    }

    private void AddPaymentRow(string method, string currency, decimal? amount)
    {
        var row = new CheckoutPaymentRow
        {
            Methods = PaymentMethods,
            CurrencyOptions = Currencies,
            SelectedMethod = PaymentMethods.FirstOrDefault(x => string.Equals(x.Code, method, StringComparison.OrdinalIgnoreCase))
                             ?? PaymentMethods[0],
            SelectedCurrency = FindCurrency(currency) ?? FindCurrency(BaseCurrency),
            AmountText = amount is > 0 ? QuantityInput.Format(amount.Value) : ""
        };
        row.PropertyChanged += OnPaymentPropertyChanged;
        Payments.Add(row);
    }

    private void OnPaymentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_initializingPayments || sender is not CheckoutPaymentRow row) return;
        if (e.PropertyName is not (nameof(CheckoutPaymentRow.SelectedMethod)
            or nameof(CheckoutPaymentRow.SelectedCurrency)
            or nameof(CheckoutPaymentRow.AmountText))) return;

        if (e.PropertyName == nameof(CheckoutPaymentRow.SelectedMethod)
            && row.SelectedMethod?.Code == "Bonus"
            && row.SelectedCurrency?.IsBase != true)
            row.SelectedCurrency = FindCurrency(BaseCurrency);
        Recalc();
    }

    private void ReplaceCurrencies(IEnumerable<CurrencyDto> currencies)
    {
        Currencies.Clear();
        foreach (var currency in currencies.Where(x => x.IsEnabled))
            Currencies.Add(currency);
        if (Currencies.All(x => !x.IsBase))
            Currencies.Insert(0, new CurrencyDto(BaseCurrency, BaseCurrency, true, true, true, true, 1, DateTime.UtcNow));
    }

    private CurrencyDto? FindCurrency(string code) => Currencies.FirstOrDefault(
        x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));

    private List<SalePaymentRequest>? BuildPaymentRequests()
    {
        if (!IsMulticurrency) return null;
        return Payments.Where(x => x.Amount > 0 && x.SelectedMethod is not null && x.SelectedCurrency is not null)
            .Select(x => new SalePaymentRequest(x.SelectedMethod!.Code, x.SelectedCurrency!.Code, x.Amount))
            .ToList();
    }

    private List<ParticipantSelectionRequest>? BuildParticipantRequests()
    {
        if (!string.IsNullOrEmpty(_code))
            return null;
        return _localCart.Participants.Count == 0
            ? null
            : _localCart.Participants
                .Select(x => new ParticipantSelectionRequest(x.RoleDefinitionId, x.PartyId))
                .ToList();
    }

    private (decimal Cash, decimal Card, decimal Bonus) PaymentAggregates()
    {
        if (!IsMulticurrency)
            return (Parse(CashText), Parse(CardText), Parse(BonusText));
        return (
            Payments.Where(x => x.SelectedMethod?.Code == "Cash").Sum(x => x.AmountBase),
            Payments.Where(x => x.SelectedMethod?.Code is "Card" or "Transfer" or "Bank").Sum(x => x.AmountBase),
            Payments.Where(x => x.SelectedMethod?.Code == "Bonus").Sum(x => x.AmountBase));
    }

    private bool ValidatePayments()
    {
        if (IsMulticurrency)
        {
            HasPaymentRateError = Payments.Any(x => x.Amount > 0 && x.EffectiveRate <= 0);
            if (HasPaymentRateError)
            {
                Error = Loc.Instance["currency_rate_required"];
                NotifyError();
                return false;
            }
            if (Payments.Any(x => x.SelectedMethod?.Code == "Bonus" && x.SelectedCurrency?.IsBase != true))
            {
                Error = Loc.Instance["bonus_base_currency_only"];
                NotifyError();
                return false;
            }
        }
        if (HasInvalidChange)
        {
            Error = Loc.Instance["change_cash_only"];
            NotifyError();
            return false;
        }
        return true;
    }

    private decimal Paid => IsMulticurrency
        ? Payments.Sum(x => x.AmountBase)
        : Parse(CashText) + Parse(CardText) + Parse(BonusText);

    private static decimal Parse(string? text) => decimal.TryParse(
        text?.Trim().Replace(',', '.'),
        NumberStyles.Number,
        CultureInfo.InvariantCulture,
        out var value) && value > 0 ? value : 0;

    private void Recalc()
    {
        HasCustomer = _customerId is not null;
        HasNote = !string.IsNullOrEmpty(NoteText);
        TotalText = $"{_totalAmount:N0} {BaseCurrency}";
        var paid = Paid;
        PaidText = $"{paid:N0} {BaseCurrency}";
        var excess = Math.Max(0, paid - _totalAmount);
        if ((!HasCustomer || excess <= 0) && KeepExcessAsCredit)
            KeepExcessAsCredit = false;
        var credit = KeepExcessAsCredit && HasCustomer ? excess : 0;
        ExcessText = excess > 0 ? $"{excess:N0} {BaseCurrency}" : "";
        var change = excess - credit;
        var aggregates = PaymentAggregates();
        HasInvalidChange = change > 0 && aggregates.Card + aggregates.Bonus > _totalAmount + credit;
        ChangeText = change > 0 ? $"{change:N0} {BaseCurrency}" : "";
        var debt = _totalAmount - paid;
        DebtText = debt > 0 ? $"{debt:N0} {BaseCurrency}" : "";
        HasPaymentRateError = IsMulticurrency && Payments.Any(x => x.Amount > 0 && x.EffectiveRate <= 0);
        HasRateWarning = IsMulticurrency && Payments.Any(x => x.Amount > 0 && x.IsRateStale);
        OnPropertyChanged(nameof(CanStoreExcessAsCredit));
        NotifyPaymentState();
    }

    private decimal CreditAmount() => KeepExcessAsCredit && HasCustomer
        ? Math.Max(0, Paid - _totalAmount)
        : 0;

    private void NotifyPaymentState()
    {
        OnPropertyChanged(nameof(CanAddPayment));
        OnPropertyChanged(nameof(HasError));
    }

    private void NotifyError() => OnPropertyChanged(nameof(HasError));

    private static string Describe(Exception exception) => exception is ApiException api
        ? ApiErrors.Describe(api)
        : exception is InvalidOperationException ? exception.Message : Loc.Instance["err_no_connection"];

    private static bool IsConnectionFailure(Exception exception) =>
        exception is HttpRequestException or TaskCanceledException
        || exception.InnerException is HttpRequestException or System.Net.Sockets.SocketException;
}

public sealed record CheckoutLine(string Name, decimal Quantity, decimal UnitPrice, decimal LineTotal)
{
    public string QtyPriceText => $"{Quantity:0.###} × {UnitPrice:N0}";
    public string LineTotalText => $"{LineTotal:N0}";
}

public sealed record CheckoutParticipantLine(string RoleLabel, string PartyName);

public sealed record CheckoutPaymentMethod(string Code, string Label);

public partial class CheckoutPaymentRow : ObservableObject
{
    public required IReadOnlyList<CheckoutPaymentMethod> Methods { get; init; }
    public required IReadOnlyList<CurrencyDto> CurrencyOptions { get; init; }

    [ObservableProperty] private CheckoutPaymentMethod? _selectedMethod;
    [ObservableProperty] private CurrencyDto? _selectedCurrency;
    [ObservableProperty] private string _amountText = "";

    public decimal Amount => decimal.TryParse(
        AmountText.Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
        ? Math.Max(0, value)
        : 0;
    public decimal EffectiveRate => SelectedCurrency?.IsBase == true ? 1 : SelectedCurrency?.Rate ?? 0;
    public decimal AmountBase => Math.Round(Amount * EffectiveRate, 2);
    public string BaseAmountText => EffectiveRate > 0 ? $"≈ {AmountBase:N0}" : Loc.Instance["rate_missing"];
    public bool IsRateStale => SelectedCurrency is { IsBase: false, RateAt: { } at }
                               && DateTime.UtcNow - at.ToUniversalTime() > TimeSpan.FromHours(36);

    private void NotifyCalculated()
    {
        OnPropertyChanged(nameof(Amount));
        OnPropertyChanged(nameof(EffectiveRate));
        OnPropertyChanged(nameof(AmountBase));
        OnPropertyChanged(nameof(BaseAmountText));
        OnPropertyChanged(nameof(IsRateStale));
    }

    partial void OnAmountTextChanged(string value) => NotifyCalculated();
    partial void OnSelectedCurrencyChanged(CurrencyDto? value) => NotifyCalculated();
}
