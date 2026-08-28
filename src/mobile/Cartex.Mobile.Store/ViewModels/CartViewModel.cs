using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Querying;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.Partners;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Store.ViewModels;

public partial class CartViewModel : AccessAwareViewModel
{
    private readonly CartStore _cart;
    private readonly IPartnersApi _partnersApi;
    private readonly MobileOfflineService _offline;

    public RangeObservableCollection<CartLine> Lines { get; } = [];
    public MobileCustomerPicker CustomerPicker { get; }
    public ObservableCollection<ParticipantRoleSelectionRow> ParticipantRoles { get; } = [];
    public ObservableCollection<PartnerDto> PartnerResults { get; } = [];

    [ObservableProperty] private bool _isEmpty = true;
    [ObservableProperty] private bool _canUndoClear;

    private List<CartLine> _undo = [];
    private CancellationTokenSource? _undoToken;
    [ObservableProperty] private string _totalText = "";
    [ObservableProperty] private string? _customerName;
    [ObservableProperty] private bool _hasCustomer;
    [ObservableProperty] private bool _hasPartnerResults;
    [ObservableProperty] private bool _isParticipantModalOpen;
    [ObservableProperty] private ParticipantRoleSelectionRow? _selectedParticipantRole;
    [ObservableProperty] private string _participantSearch = "";
    
    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private bool _isProductModalOpen;
    [ObservableProperty] private CartLine? _selectedProduct;
    [ObservableProperty] private string _selectedProductImage = "";
    [ObservableProperty] private string _selectedQuantityText = "";
    [ObservableProperty] private string _selectedPriceText = "";
    [ObservableProperty] private string _selectedOriginalPriceText = "";
    [ObservableProperty] private bool _hasPriceOverride;
    [ObservableProperty] private string _selectedAmountText = "";
    [ObservableProperty] private bool _canEnterAmount;

    partial void OnSelectedProductChanged(CartLine? value) => SyncSelectedTexts();

    // Ayni shu qator qayta tanlanganda setter o'zgarishsiz o'tib ketadi, matnlar esa
    // qatordagi eski qiymatda qolib keyingi commit'da uni qaytarib yozadi — shuning uchun
    // tanlash nuqtalarida sinxron har doim qo'lda chaqiriladi.
    private void SyncSelectedTexts()
    {
        SelectedQuantityText = SelectedProduct?.Quantity.ToString("0.###") ?? "0";
        SelectedPriceText = SelectedProduct?.UnitPrice.ToString("0.##") ?? "0";
        SelectedAmountText = SelectedProduct?.LineTotal.ToString("0") ?? "0";
        CanEnterAmount = SelectedProduct?.AllowsAmountEntry == true;
        RefreshPriceState();
    }

    private void RefreshPriceState()
    {
        HasPriceOverride = SelectedProduct?.PriceOverride is not null;
        SelectedOriginalPriceText = SelectedProduct is { } line
            ? string.Format(Loc.Instance["original_price_fmt"], $"{line.OriginalPrice:N0}")
            : "";
    }

    private CancellationTokenSource? _partnerSearchCts;
    private readonly ImageUrlBuilder _images;
    private bool _rolesLoaded;

    public bool HasParticipantRoles => ParticipantRoles.Count > 0;
    public bool CanUseCart => Access.CanUseCart;
    public bool CanOverridePrice => Access.CanOverridePrice;
    public bool CanUseBuyerForSelectedRole =>
        SelectedParticipantRole?.CanEqualBuyer == true && _cart.CustomerId.HasValue;

    public CartViewModel(
        CartStore cart,
        ICustomersApi customersApi,
        IPartnersApi partnersApi,
        AccessState access,
        ImageUrlBuilder images,
        MobileOfflineService offline) : base(access)
    {
        _cart = cart;
        _partnersApi = partnersApi;
        _images = images;
        _offline = offline;
        CustomerPicker = new MobileCustomerPicker(customersApi, offline, access,
            (id, name) => _cart.SetCustomer(id, name));
        ObserveAccess(nameof(CanUseCart), nameof(CanOverridePrice));
    }

    public async Task AppearAsync()
    {
        await _offline.StartAsync();
        _cart.Changed += Refresh;
        Refresh();
        if (!_rolesLoaded)
            await LoadParticipantRolesAsync();
    }

    public void Disappear() => _cart.Changed -= Refresh;

    [RelayCommand]
    private async Task ClearCartAsync()
    {
        if (IsEmpty) return;
        var page = Shell.Current?.CurrentPage;
        if (page is null) return;
        var confirmed = await page.DisplayAlertAsync(
            Loc.Instance["cart_clear_title"],
            string.Format(Loc.Instance["cart_clear_body"], Lines.Count),
            Loc.Instance["cart_clear_confirm"],
            Loc.Instance["cancel"]);
        if (!confirmed) return;

        _undo = [.. _cart.Lines];
        _cart.Clear();
        CanUndoClear = true;
        var token = _undoToken = new CancellationTokenSource();
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), token.Token);
            CanUndoClear = false;
        }
        catch (OperationCanceledException)
        {
        }
    }

    [RelayCommand]
    private void UndoClear()
    {
        Debounce.Cancel(ref _undoToken);
        CanUndoClear = false;
        if (_undo.Count == 0) return;
        _cart.Restore(_undo);
        _undo = [];
    }

    private void Refresh()
    {
        if (!Lines.SequenceEqual(_cart.Lines))
            Lines.ReplaceAll(_cart.Lines);
        IsEmpty = Lines.Count == 0;
        TotalText = $"{_cart.Total:N0} UZS";
        CustomerName = _cart.CustomerName;
        HasCustomer = !string.IsNullOrEmpty(_cart.CustomerName);
        CustomerPicker.Sync(_cart.CustomerId, _cart.CustomerName);
        foreach (var role in ParticipantRoles)
            role.Replace(_cart.Participants);
        OnPropertyChanged(nameof(CanUseBuyerForSelectedRole));
    }

    partial void OnParticipantSearchChanged(string value) => _ = SearchPartnersAsync(value);
    partial void OnSelectedParticipantRoleChanged(ParticipantRoleSelectionRow? value) =>
        OnPropertyChanged(nameof(CanUseBuyerForSelectedRole));

    [RelayCommand]
    private void Increment(CartLine line) =>
        _cart.SetQuantity(line, line.Quantity + 1m);

    [RelayCommand]
    private void Decrement(CartLine line)
    {
        var next = Math.Max(1m, line.Quantity - 1m);
        if (next < line.Quantity)
            _cart.SetQuantity(line, next);
    }
    
    [RelayCommand]
    private void DecrementSelected()
    {
        if (SelectedProduct is not null)
        {
            Decrement(SelectedProduct);
            SelectedQuantityText = QuantityInput.Format(SelectedProduct.Quantity);
        }
    }

    [RelayCommand]
    private void IncrementSelected()
    {
        if (SelectedProduct is not null)
        {
            Increment(SelectedProduct);
            SelectedQuantityText = QuantityInput.Format(SelectedProduct.Quantity);
        }
    }

    [RelayCommand]
    private void Remove(CartLine line)
    {
        _cart.Remove(line);
        if (SelectedProduct == line)
            IsProductModalOpen = false;
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        if (SelectedProduct is not null)
            Remove(SelectedProduct);
    }

    [RelayCommand]
    private void OpenParticipantModal(ParticipantRoleSelectionRow role)
    {
        if (!role.CanAdd)
        {
            Ui.Toast(Loc.Instance["participant_limit_reached"]);
            return;
        }
        SelectedParticipantRole = role;
        ParticipantSearch = "";
        PartnerResults.Clear();
        HasPartnerResults = false;
        IsParticipantModalOpen = true;
    }

    [RelayCommand]
    private void CloseParticipantModal()
    {
        Debounce.Cancel(ref _partnerSearchCts);
        IsParticipantModalOpen = false;
        ParticipantSearch = "";
        PartnerResults.Clear();
        HasPartnerResults = false;
    }

    [RelayCommand]
    private void PickPartner(PartnerDto partner)
    {
        var role = SelectedParticipantRole;
        if (role is null) return;
        if (!_cart.AddParticipant(role.Id, partner.PartyId, partner.FullName, role.Label, role.MaxCount))
        {
            Ui.Toast(Loc.Instance["participant_limit_reached"]);
            return;
        }
        role.Add(new CartParticipantDraft(role.Id, partner.PartyId, partner.FullName, role.Label));
        CloseParticipantModal();
    }

    [RelayCommand]
    private void RemoveParticipant(CartParticipantDraft participant)
    {
        _cart.RemoveParticipant(participant.RoleDefinitionId, participant.PartyId);
        ParticipantRoles.FirstOrDefault(x => x.Id == participant.RoleDefinitionId)?.Remove(participant.PartyId);
    }

    [RelayCommand]
    private async Task UseBuyerAsParticipantAsync()
    {
        var role = SelectedParticipantRole;
        if (role is null || !role.CanEqualBuyer || !_cart.CustomerId.HasValue) return;
        IsBusy = true;
        try
        {
            IReadOnlyList<PartnerDto> matches = _offline.ShouldUseOffline
                ? await _offline.SearchPartnersAsync(_cart.CustomerName ?? "", 50)
                : await _partnersApi.GetAsync(_cart.CustomerName, true, 1, 50);
            var partner = _offline.ShouldUseOffline
                ? await _offline.FindPartnerByCustomerAsync(_cart.CustomerId.Value)
                : matches.FirstOrDefault(x => x.CustomerId == _cart.CustomerId);
            if (partner is null && Access.CanEditPartners)
            {
                if (_offline.ShouldUseOffline)
                {
                    Ui.Toast(Loc.Instance["offline_mutation_blocked"]);
                    return;
                }
                await _partnersApi.CreateAsync(new CreatePartnerRequest(
                    _cart.CustomerName ?? Loc.Instance["customer"], CustomerId: _cart.CustomerId));
                matches = await _partnersApi.GetAsync(_cart.CustomerName, true, 1, 50);
                partner = matches.FirstOrDefault(x => x.CustomerId == _cart.CustomerId);
            }
            if (partner is null)
            {
                Ui.Toast(Loc.Instance["buyer_partner_missing"]);
                return;
            }
            PickPartner(partner);
        }
        catch { Ui.Toast(Loc.Instance["err_no_connection"]); }
        finally { IsBusy = false; }
    }

    // Ochiq swipe'ni yopish uchun qator bosilganda chaqiriladi; sahifa biror drawer
    // yopilganini qaytaradi — u holda bosish faqat yopish deb qabul qilinadi.
    public Func<bool>? RowInteracted { get; set; }

    // Qator bosilganda narx tahriri ochiladi (desktopdagi qatorda tahrirlashga mos);
    // ruxsat bo'lmasa avvalgidek mahsulot oynasi ochiladi.
    [RelayCommand]
    private void ToggleExpandLine(CartLine line)
    {
        if (RowInteracted?.Invoke() == true) return;
        if (!CanOverridePrice)
        {
            OpenProductModal(line);
            return;
        }
        foreach (var other in Lines)
            if (!ReferenceEquals(other, line))
                other.IsExpanded = false;
        if (!line.IsExpanded)
        {
            SelectedProduct = line;
            SyncSelectedTexts();
        }
        line.IsExpanded = !line.IsExpanded;
    }

    [RelayCommand]
    private void OpenProductModal(CartLine line)
    {
        if (RowInteracted?.Invoke() == true) return;
        foreach (var other in Lines)
            other.IsExpanded = false;
        SelectedProduct = line;
        SyncSelectedTexts();
        IsProductModalOpen = true;
        SelectedProductImage = _images.FromKey(line.ImageKey) ?? "";
    }

    [RelayCommand]
    private void CloseProductModal() => IsProductModalOpen = false;

    [RelayCommand]
    private void SetQuantityFromText()
    {
        if (SelectedProduct is null) return;
        if (QuantityInput.TryParse(
                SelectedQuantityText,
                SelectedProduct.AllowsFractional,
                out var quantity,
                out var error))
        {
            _cart.SetQuantity(SelectedProduct, quantity);
            SelectedQuantityText = QuantityInput.Format(SelectedProduct.Quantity);
        }
        else
        {
            SelectedQuantityText = QuantityInput.Format(SelectedProduct.Quantity);
            Ui.Toast(Loc.Instance[error switch
            {
                QuantityInputError.MustBePositive => "quantity_positive_required",
                QuantityInputError.FractionNotAllowed => "quantity_integer_required",
                _ => "quantity_invalid"
            }]);
        }
    }

    [RelayCommand]
    private void SetPriceFromText()
    {
        if (SelectedProduct is null || !CanOverridePrice) return;
        var previous = SelectedProduct.UnitPrice;
        // Maydon bo'sh qoldirilsa xato emas, narx nolga tushadi. Narxsiz qator savdoni
        // yakunlashda to'xtatiladi - bepul mahsulot sotuvga qo'yilmaydi.
        if (string.IsNullOrWhiteSpace(SelectedPriceText)) SelectedPriceText = "0";
        if (decimal.TryParse(
                SelectedPriceText.Trim().Replace(',', '.'),
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var price)
            && price >= 0
            && _cart.SetPrice(SelectedProduct, price))
        {
            SelectedPriceText = price.ToString("0.##");
            // Tahrir yopilish asnosida chala terilgan narx ham saqlanib qolishi mumkin —
            // natija ko'zga tashlansin, aks holda savdo noto'g'ri narxda jim ketadi.
            if (price != previous)
                Ui.Toast($"{Loc.Instance["price"]}: {price:N0}");
        }
        else
        {
            SelectedPriceText = SelectedProduct.UnitPrice.ToString("0.##");
        }
        RefreshPriceState();
    }

    // Summadan miqdor: kassir "20 ming so'mlik" deb yozadi, miqdorni tizim hisoblaydi.
    [RelayCommand]
    private void SetAmountFromText()
    {
        if (SelectedProduct is null || !CanEnterAmount) return;
        if (decimal.TryParse(
                SelectedAmountText.Trim().Replace(',', '.'),
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var amount)
            && _cart.SetAmount(SelectedProduct, amount) is { } quantity)
        {
            SelectedQuantityText = quantity.ToString("0.###");
            Ui.Toast($"{quantity:0.###} {SelectedProduct.UnitName}");
        }
        SelectedAmountText = SelectedProduct.LineTotal.ToString("0");
    }

    [RelayCommand]
    private Task NextAsync()
    {
        if (_cart.Lines.Count == 0)
        {
            Ui.Toast(Loc.Instance["err_no_items"]);
            return Task.CompletedTask;
        }
        var missing = ParticipantRoles.FirstOrDefault(x => x.IsRequired && !x.HasSelections);
        if (missing is not null)
        {
            Ui.Toast(string.Format(Loc.Instance["participant_required_fmt"], missing.Label));
            return Task.CompletedTask;
        }
        return Shell.Current.GoToAsync("checkout");
    }

    private async Task LoadParticipantRolesAsync()
    {
        _rolesLoaded = true;
        if (!Access.CanViewPartners) return;
        try
        {
            var roles = _offline.ShouldUseOffline
                ? await _offline.GetParticipantRolesAsync()
                : await _partnersApi.GetRolesAsync(false);
            ParticipantRoles.Clear();
            foreach (var role in roles.Where(x => x.IsEnabled && x.AppliesToCart).OrderBy(x => x.SortOrder))
            {
                var row = new ParticipantRoleSelectionRow(role);
                row.Replace(_cart.Participants);
                ParticipantRoles.Add(row);
            }
            OnPropertyChanged(nameof(HasParticipantRoles));
        }
        catch
        {
            _offline.MarkServerUnavailable();
            if (_offline.IsEnabled)
            {
                var roles = await _offline.GetParticipantRolesAsync();
                ParticipantRoles.Clear();
                foreach (var role in roles.OrderBy(x => x.SortOrder))
                {
                    var row = new ParticipantRoleSelectionRow(role);
                    row.Replace(_cart.Participants);
                    ParticipantRoles.Add(row);
                }
                OnPropertyChanged(nameof(HasParticipantRoles));
            }
            else _rolesLoaded = false;
        }
    }

    private async Task SearchPartnersAsync(string term)
    {
        var owner = Debounce.Restart(ref _partnerSearchCts);
        PartnerResults.Clear();
        HasPartnerResults = false;
        if (!IsParticipantModalOpen || string.IsNullOrWhiteSpace(term)) return;
        try
        {
            await Task.Delay(300, owner.Token);
            var rows = _offline.ShouldUseOffline
                ? await _offline.SearchPartnersAsync(term.Trim(), 30)
                : await _partnersApi.GetAsync(term.Trim(), true, 1, 30);
            if (owner.IsCancellationRequested) return;
            var selectedPartyIds = SelectedParticipantRole?.Selections.Select(x => x.PartyId).ToHashSet() ?? [];
            foreach (var row in rows.Where(x => !selectedPartyIds.Contains(x.PartyId)))
                PartnerResults.Add(row);
            HasPartnerResults = PartnerResults.Count > 0;
        }
        catch (OperationCanceledException) { }
        catch
        {
            _offline.MarkServerUnavailable();
            if (_offline.IsEnabled && !owner.IsCancellationRequested)
            {
                var selectedPartyIds = SelectedParticipantRole?.Selections.Select(x => x.PartyId).ToHashSet() ?? [];
                foreach (var row in (await _offline.SearchPartnersAsync(term.Trim(), 30))
                             .Where(x => !selectedPartyIds.Contains(x.PartyId)))
                    PartnerResults.Add(row);
                HasPartnerResults = PartnerResults.Count > 0;
            }
        }
    }
}
