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

public partial class CartViewModel : ObservableObject
{
    private readonly CartStore _cart;
    private readonly ICustomersApi _customersApi;
    private readonly IPartnersApi _partnersApi;
    private readonly MobilePermissions _permissions;
    private readonly MobileOfflineService _offline;

    public ObservableCollection<CartLine> Lines { get; } = [];
    public ObservableCollection<CustomerDto> Customers { get; } = [];
    public ObservableCollection<ParticipantRoleSelectionRow> ParticipantRoles { get; } = [];
    public ObservableCollection<PartnerDto> PartnerResults { get; } = [];

    [ObservableProperty] private bool _isEmpty = true;
    [ObservableProperty] private string _totalText = "";
    [ObservableProperty] private string? _customerName;
    [ObservableProperty] private bool _hasCustomer;
    [ObservableProperty] private string _customerSearch = "";
    [ObservableProperty] private bool _hasCustomers;
    [ObservableProperty] private bool _hasPartnerResults;
    [ObservableProperty] private bool _isParticipantModalOpen;
    [ObservableProperty] private ParticipantRoleSelectionRow? _selectedParticipantRole;
    [ObservableProperty] private string _participantSearch = "";
    
    [ObservableProperty] private bool _isCustomerModalOpen;
    [ObservableProperty] private string _newCustomerName = "";
    [ObservableProperty] private string _newCustomerPhone = "";
    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private bool _isProductModalOpen;
    [ObservableProperty] private CartLine? _selectedProduct;
    [ObservableProperty] private string _selectedProductImage = "";
    [ObservableProperty] private string _selectedQuantityText = "";

    partial void OnSelectedProductChanged(CartLine? value) =>
        SelectedQuantityText = value?.Quantity.ToString("0.###") ?? "0";

    private CancellationTokenSource? _searchCts;
    private CancellationTokenSource? _partnerSearchCts;
    private readonly ImageUrlBuilder _images;
    private bool _rolesLoaded;

    public bool HasParticipantRoles => ParticipantRoles.Count > 0;
    public bool CanUseBuyerForSelectedRole =>
        SelectedParticipantRole?.CanEqualBuyer == true && _cart.CustomerId.HasValue;

    public CartViewModel(
        CartStore cart,
        ICustomersApi customersApi,
        IPartnersApi partnersApi,
        MobilePermissions permissions,
        ImageUrlBuilder images,
        MobileOfflineService offline)
    {
        _cart = cart;
        _customersApi = customersApi;
        _partnersApi = partnersApi;
        _permissions = permissions;
        _images = images;
        _offline = offline;
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

    private void Refresh()
    {
        if (!Lines.SequenceEqual(_cart.Lines))
        {
            Lines.Clear();
            foreach (var line in _cart.Lines)
                Lines.Add(line);
        }
        IsEmpty = Lines.Count == 0;
        TotalText = $"{_cart.Total:N0} UZS";
        CustomerName = _cart.CustomerName;
        HasCustomer = !string.IsNullOrEmpty(_cart.CustomerName);
        foreach (var role in ParticipantRoles)
            role.Replace(_cart.Participants);
        OnPropertyChanged(nameof(CanUseBuyerForSelectedRole));
    }

    partial void OnCustomerSearchChanged(string value) => _ = SearchCustomersAsync(value);
    partial void OnParticipantSearchChanged(string value) => _ = SearchPartnersAsync(value);
    partial void OnSelectedParticipantRoleChanged(ParticipantRoleSelectionRow? value) =>
        OnPropertyChanged(nameof(CanUseBuyerForSelectedRole));

    private async Task SearchCustomersAsync(string term)
    {
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();
        if (string.IsNullOrWhiteSpace(term))
        {
            Customers.Clear();
            HasCustomers = false;
            return;
        }
        try
        {
            await Task.Delay(350, cts.Token);
            IReadOnlyList<CustomerDto> rows;
            if (_offline.ShouldUseOffline)
                rows = await _offline.SearchCustomersAsync(term, 10);
            else
            {
                var response = await _customersApi.QueryAsync(QueryRequest.Create().Page(1, 10).Search(term).Build());
                rows = response.Content ?? [];
            }
            if (cts.IsCancellationRequested) return;
            Customers.Clear();
            foreach (var customer in rows)
                Customers.Add(customer);
            HasCustomers = Customers.Count > 0;
        }
        catch (OperationCanceledException) { }
        catch
        {
            _offline.MarkServerUnavailable();
            if (!_offline.IsEnabled || cts.IsCancellationRequested) return;
            Customers.Clear();
            foreach (var customer in await _offline.SearchCustomersAsync(term, 10))
                Customers.Add(customer);
            HasCustomers = Customers.Count > 0;
        }
    }

    [RelayCommand]
    private void Increment(CartLine line) =>
        _cart.SetQuantity(line.VariantId, line.Quantity + line.IncrementStep);

    [RelayCommand]
    private void Decrement(CartLine line)
    {
        var next = Math.Max(line.QuantityStep, line.Quantity - line.IncrementStep);
        if (next < line.Quantity)
            _cart.SetQuantity(line.VariantId, next);
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
        _cart.Remove(line.VariantId);
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
    private void PickCustomer(CustomerDto customer)
    {
        _cart.SetCustomer(customer.Id, customer.FullName);
        CustomerSearch = "";
        Customers.Clear();
        HasCustomers = false;
    }

    [RelayCommand]
    private void ClearCustomer() => _cart.SetCustomer(null, null);

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
        _partnerSearchCts?.Cancel();
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
            if (partner is null && _permissions.Has("partners.edit"))
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

    [RelayCommand]
    private void OpenCustomerModal()
    {
        NewCustomerName = CustomerSearch;
        NewCustomerPhone = "";
        IsCustomerModalOpen = true;
    }

    [RelayCommand]
    private void CloseCustomerModal() => IsCustomerModalOpen = false;

    [RelayCommand]
    private async Task SaveCustomerAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCustomerName) || string.IsNullOrWhiteSpace(NewCustomerPhone))
        {
            Ui.Toast(Loc.Instance["err_fill_all"]);
            return;
        }
        IsBusy = true;
        try
        {
            if (_offline.ShouldUseOffline)
            {
                Ui.Toast(Loc.Instance["offline_mutation_blocked"]);
                return;
            }
            var request = new CreateCustomerRequest(NewCustomerName, NewCustomerPhone, null, 0);
            var id = await _customersApi.CreateAsync(request);
            _cart.SetCustomer(id, NewCustomerName);
            IsCustomerModalOpen = false;
            CustomerSearch = "";
            HasCustomers = false;
        }
        catch { Ui.Toast(Loc.Instance["err_no_connection"]); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void OpenProductModal(CartLine line)
    {
        SelectedProduct = line;
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
                SelectedProduct.QuantityStep,
                SelectedProduct.AllowsFractional,
                out var quantity,
                out var error))
        {
            _cart.SetQuantity(SelectedProduct.VariantId, quantity);
            SelectedQuantityText = QuantityInput.Format(quantity);
        }
        else
        {
            SelectedQuantityText = QuantityInput.Format(SelectedProduct.Quantity);
            Ui.Toast(Loc.Instance[error switch
            {
                QuantityInputError.MustBePositive => "quantity_positive_required",
                QuantityInputError.FractionNotAllowed => "quantity_integer_required",
                QuantityInputError.StepMismatch => "quantity_step_invalid",
                _ => "quantity_invalid"
            }]);
        }
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
        if (!_permissions.Has("partners.view")) return;
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
        _partnerSearchCts?.Cancel();
        var owner = _partnerSearchCts = new CancellationTokenSource();
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
