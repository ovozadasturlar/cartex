using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.Partners;
using Cartex.Shared.Models.Settings;
using Cartex.Shared.Models.TradeCases;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace Cartex.Mobile.Store.ViewModels;

public partial class CaseCreateViewModel(
    ICustomersApi customersApi,
    ITradeCasesApi tradeCasesApi,
    ISettingsApi settingsApi,
    IPartnersApi partnersApi,
    MobilePermissions permissions,
    WarehouseContext warehouse) : ObservableObject, IQueryAttributable
{
    public IReadOnlyList<CaseChoice> Workflows { get; } =
    [new("CustodyUntilSettlement", Loc.Instance["workflow_custody"]), new("ImmediateInvoice", Loc.Instance["workflow_immediate"])];
    public IReadOnlyList<CaseChoice> PricePolicies { get; } =
    [new("SnapshotAtIssue", Loc.Instance["price_snapshot_issue"]), new("CurrentAtSettlement", Loc.Instance["price_at_settlement"])];
    public ObservableCollection<ParticipantRoleSelectionRow> ParticipantRoles { get; } = [];
    public ObservableCollection<PartnerDto> PartnerResults { get; } = [];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isLoaded;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string _customerName = "";
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _siteAddress = "";
    [ObservableProperty] private string _note = "";
    [ObservableProperty] private CaseChoice? _selectedWorkflow;
    [ObservableProperty] private CaseChoice? _selectedPricePolicy;
    [ObservableProperty] private bool _allowWorkflowOverride;
    [ObservableProperty] private bool _allowPricePolicyOverride;
    [ObservableProperty] private bool _requireSiteAddress;
    [ObservableProperty] private string _pageTitle = "";
    [ObservableProperty] private bool _isParticipantModalOpen;
    [ObservableProperty] private ParticipantRoleSelectionRow? _selectedParticipantRole;
    [ObservableProperty] private string _participantSearch = "";
    [ObservableProperty] private bool _hasPartnerResults;

    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public bool HasParticipantRoles => ParticipantRoles.Count > 0;
    public bool CanUseBuyerForSelectedRole => SelectedParticipantRole?.CanEqualBuyer == true;

    private long _customerId;
    private TradeCaseSettingsDto? _settings;
    private readonly string _idempotencyKey = Guid.NewGuid().ToString("N");
    private CancellationTokenSource? _partnerSearchCts;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("customerId", out var value)) long.TryParse(value.ToString(), out _customerId);
    }

    public async Task AppearAsync()
    {
        if (IsLoaded || IsLoading || _customerId <= 0) return;
        IsLoading = true;
        try
        {
            var customerTask = customersApi.GetByIdAsync(_customerId);
            var settingsTask = settingsApi.GetTradeCaseSettingsAsync();
            var rolesTask = permissions.Has("partners.view")
                ? partnersApi.GetRolesAsync(false)
                : Task.FromResult(new List<ParticipantRoleDto>());
            await Task.WhenAll(customerTask, settingsTask, rolesTask);
            CustomerName = (await customerTask).FullName;
            _settings = await settingsTask;
            PageTitle = string.Format(Loc.Instance["new_named_fmt"], _settings.SingularLabel);
            SelectedWorkflow = Workflows.FirstOrDefault(x => x.Code == _settings.DefaultWorkflow) ?? Workflows[0];
            SelectedPricePolicy = PricePolicies.FirstOrDefault(x => x.Code == _settings.DefaultPricePolicy) ?? PricePolicies[0];
            AllowWorkflowOverride = _settings.AllowWorkflowOverride;
            AllowPricePolicyOverride = _settings.AllowPricePolicyOverride;
            RequireSiteAddress = _settings.RequireSiteAddress;
            ParticipantRoles.Clear();
            foreach (var role in (await rolesTask)
                         .Where(x => x.IsEnabled && x.AppliesToTradeCase)
                         .OrderBy(x => x.SortOrder))
                ParticipantRoles.Add(new ParticipantRoleSelectionRow(role));
            IsLoaded = true;
        }
        catch (Exception ex) { Error = Describe(ex); }
        finally { IsLoading = false; Notify(); }
    }

    partial void OnParticipantSearchChanged(string value) => _ = SearchPartnersAsync(value);
    partial void OnSelectedParticipantRoleChanged(ParticipantRoleSelectionRow? value) =>
        OnPropertyChanged(nameof(CanUseBuyerForSelectedRole));

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
        role.Add(new CartParticipantDraft(role.Id, partner.PartyId, partner.FullName, role.Label));
        CloseParticipantModal();
    }

    [RelayCommand]
    private void RemoveParticipant(CartParticipantDraft participant) =>
        ParticipantRoles.FirstOrDefault(x => x.Id == participant.RoleDefinitionId)?.Remove(participant.PartyId);

    [RelayCommand]
    private async Task UseBuyerAsParticipantAsync()
    {
        var role = SelectedParticipantRole;
        if (role is null || !role.CanEqualBuyer) return;
        IsBusy = true;
        try
        {
            var matches = await partnersApi.GetAsync(CustomerName, true, 1, 50);
            var partner = matches.FirstOrDefault(x => x.CustomerId == _customerId);
            if (partner is null && permissions.Has("partners.edit"))
            {
                await partnersApi.CreateAsync(new CreatePartnerRequest(CustomerName, CustomerId: _customerId));
                matches = await partnersApi.GetAsync(CustomerName, true, 1, 50);
                partner = matches.FirstOrDefault(x => x.CustomerId == _customerId);
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
    private async Task SaveAsync()
    {
        if (_settings is null || IsBusy) return;
        if (string.IsNullOrWhiteSpace(Title) || RequireSiteAddress && string.IsNullOrWhiteSpace(SiteAddress))
        {
            Error = Loc.Instance["err_fill_all"]; Notify(); return;
        }
        var missing = ParticipantRoles.FirstOrDefault(x => x.IsRequired && !x.HasSelections);
        if (missing is not null)
        {
            Error = string.Format(Loc.Instance["participant_required_fmt"], missing.Label);
            Notify();
            return;
        }
        if (!await warehouse.EnsureSelectedAsync()) { Error = Loc.Instance["warehouse_none"]; Notify(); return; }

        IsBusy = true;
        Error = null;
        try
        {
            var result = await tradeCasesApi.CreateAsync(new CreateTradeCaseRequest(
                _customerId,
                warehouse.WarehouseId!.Value,
                Title.Trim(),
                string.IsNullOrWhiteSpace(SiteAddress) ? null : SiteAddress.Trim(),
                SelectedWorkflow?.Code,
                SelectedPricePolicy?.Code,
                Note: string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(),
                IdempotencyKey: _idempotencyKey,
                Participants: ParticipantRoles.SelectMany(x => x.Selections)
                    .Select(x => new ParticipantSelectionRequest(x.RoleDefinitionId, x.PartyId))
                    .ToList()));
            Ui.Toast(string.Format(Loc.Instance["case_created_fmt"], result.CaseNumber));
            await Shell.Current.GoToAsync($"../case/detail?id={result.Id}");
        }
        catch (Exception ex) { Error = Describe(ex); }
        finally { IsBusy = false; Notify(); }
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
            var rows = await partnersApi.GetAsync(term.Trim(), true, 1, 30);
            if (owner.IsCancellationRequested) return;
            var selected = SelectedParticipantRole?.Selections.Select(x => x.PartyId).ToHashSet() ?? [];
            foreach (var row in rows.Where(x => !selected.Contains(x.PartyId)))
                PartnerResults.Add(row);
            HasPartnerResults = PartnerResults.Count > 0;
        }
        catch (OperationCanceledException) { }
        catch { }
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(HasParticipantRoles));
    }
    private static string Describe(Exception exception) => exception is ApiException api ? ApiErrors.Describe(api) : Loc.Instance["err_no_connection"];
}

public sealed record CaseChoice(string Code, string Label);
