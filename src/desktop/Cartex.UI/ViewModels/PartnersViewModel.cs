using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Paging;
using Cartex.Shared.Models.Partners;
using Cartex.UI.Services;
using Cartex.UI.ViewModels.Common;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.UI.ViewModels;

/// A specialty the partner may hold, with the tick state the editor binds to.
public partial class SpecialtyChoice(PartnerSpecialtyDto specialty) : ObservableObject
{
    [ObservableProperty] private bool _isSelected;

    public long Id { get; } = specialty.Id;
    public string Name { get; } = specialty.Name;
}

public partial class PartnersViewModel : ViewModelBase, ILoadable
{
    private readonly IPartnersApi _api;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly AuthService _auth;
    private long _editId;

    public PartnersViewModel(IPartnersApi api, IToastService toast, IBusyService busy, AuthService auth)
    {
        _api = api;
        _toast = toast;
        _busy = busy;
        _auth = auth;
        Paging.Attach(LoadAsync);
    }

    public ObservableCollection<PartnerDto> Partners { get; } = [];
    public ObservableCollection<PartnerRewardEntryDto> Rewards { get; } = [];
    public ObservableCollection<SpecialtyChoice> Specialties { get; } = [];
    public ObservableCollection<PartnerSpecialtyDto> AllSpecialties { get; } = [];
    public PaginationState Paging { get; } = new();

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private PartnerDto? _selectedPartner;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isRewardsLoading;

    [ObservableProperty] private bool _isEditOpen;
    [ObservableProperty] private bool _isNew;
    [ObservableProperty] private string _editName = "";
    [ObservableProperty] private string _editPhone = "";
    [ObservableProperty] private string _editAddress = "";
    [ObservableProperty] private string _editNote = "";
    [ObservableProperty] private bool _editEnabled = true;

    [ObservableProperty] private bool _isSpecialtyOpen;
    [ObservableProperty] private string _specialtyName = "";

    public bool CanEdit => _auth.HasPermission("partners.edit");
    public bool HasSelection => SelectedPartner is not null;

    partial void OnSelectedPartnerChanged(PartnerDto? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        if (value is not null) _ = LoadRewardsAsync(value.Id);
        else Rewards.Clear();
    }

    partial void OnSearchTextChanged(string value) => _ = LoadAsync();

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var specialties = await _api.GetSpecialtiesAsync(includeDisabled: true);
            AllSpecialties.Clear();
            foreach (var specialty in specialties) AllSpecialties.Add(specialty);

            var page = await _api.GetAsync(search: string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
                page: Paging.Page, pageSize: Paging.PageSize);
            var keep = SelectedPartner?.Id;
            Partners.Clear();
            foreach (var partner in page) Partners.Add(partner);
            SelectedPartner = Partners.FirstOrDefault(x => x.Id == keep) ?? Partners.FirstOrDefault();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
        finally { IsLoading = false; }
    }

    private async Task LoadRewardsAsync(long partnerId)
    {
        IsRewardsLoading = true;
        try
        {
            var entries = await _api.GetRewardsAsync(partnerId, page: 1, pageSize: 50);
            Rewards.Clear();
            foreach (var entry in entries) Rewards.Add(entry);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
        finally { IsRewardsLoading = false; }
    }

    [RelayCommand]
    private void OpenNew()
    {
        if (!CanEdit) return;
        _editId = 0;
        IsNew = true;
        EditName = EditPhone = EditAddress = EditNote = "";
        EditEnabled = true;
        FillSpecialties(null);
        IsEditOpen = true;
    }

    [RelayCommand]
    private void OpenEdit()
    {
        if (!CanEdit || SelectedPartner is not { } partner) return;
        _editId = partner.Id;
        IsNew = false;
        EditName = partner.FullName;
        EditPhone = partner.Phone ?? "";
        EditAddress = partner.Address ?? "";
        EditNote = partner.Note ?? "";
        EditEnabled = partner.IsEnabled;
        FillSpecialties(partner.Specialties);
        IsEditOpen = true;
    }

    private void FillSpecialties(IReadOnlyList<PartnerSpecialtyDto>? held)
    {
        Specialties.Clear();
        foreach (var specialty in AllSpecialties.Where(x => x.IsEnabled))
            Specialties.Add(new SpecialtyChoice(specialty)
            {
                IsSelected = held?.Any(h => h.Id == specialty.Id) == true
            });
    }

    [RelayCommand]
    private void CloseEdit() => IsEditOpen = false;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!CanEdit || string.IsNullOrWhiteSpace(EditName)) { _toast.Warning(L["name"]); return; }
        var picked = Specialties.Where(x => x.IsSelected).Select(x => x.Id).ToList();
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                if (_editId == 0)
                    await _api.CreateAsync(new CreatePartnerRequest(
                        EditName.Trim(), Trim(EditPhone), null, Trim(EditAddress), null, Trim(EditNote), picked));
                else
                    await _api.UpdateAsync(_editId, new UpdatePartnerRequest(
                        EditName.Trim(), Trim(EditPhone), null, Trim(EditAddress), EditEnabled, Trim(EditNote), picked));
            }
            IsEditOpen = false;
            _toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void OpenSpecialties()
    {
        if (!CanEdit) return;
        SpecialtyName = "";
        IsSpecialtyOpen = true;
    }

    [RelayCommand]
    private void CloseSpecialties() => IsSpecialtyOpen = false;

    [RelayCommand]
    private async Task AddSpecialtyAsync()
    {
        if (!CanEdit || string.IsNullOrWhiteSpace(SpecialtyName)) return;
        try
        {
            using (_busy.Begin(L["loading"]))
                await _api.SaveSpecialtyAsync(new SavePartnerSpecialtyRequest(null, SpecialtyName.Trim()));
            SpecialtyName = "";
            await LoadAsync();
            _toast.Success(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task ToggleSpecialtyAsync(PartnerSpecialtyDto specialty)
    {
        if (!CanEdit || specialty is null) return;
        try
        {
            using (_busy.Begin(L["loading"]))
                await _api.SaveSpecialtyAsync(new SavePartnerSpecialtyRequest(
                    specialty.Id, specialty.Name, !specialty.IsEnabled, specialty.SortOrder));
            await LoadAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private static string? Trim(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
