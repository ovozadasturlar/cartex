using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Paging;
using Cartex.Shared.Models.Suppliers;
using Cartex.UI.Services;
using Cartex.UI.ViewModels.Common;

namespace Cartex.UI.ViewModels;

public partial class SuppliersViewModel : ViewModelBase, ILoadable
{
    private readonly ISuppliersApi _api;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly IExportService _export;
    private readonly AuthService _auth;
    private long _editId;

    public ObservableCollection<SupplierDto> Suppliers { get; } = [];
    public PaginationState Paging { get; } = new();

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _isEditOpen;
    [ObservableProperty] private bool _isNew;
    [ObservableProperty] private string _editName = "";
    [ObservableProperty] private string _editPhone = "";
    [ObservableProperty] private bool _isRepayOpen;
    [ObservableProperty] private decimal _repayAmount;
    [ObservableProperty] private bool _repayViaCard;
    [ObservableProperty] private SupplierDto? _repaySupplier;

    public bool IsModalOpen => IsEditOpen || IsRepayOpen;
    partial void OnIsEditOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));
    partial void OnIsRepayOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));

    public bool IsEmpty => Suppliers.Count == 0;
    public bool CanExport => _auth.HasPermission("reports.export");

    public SuppliersViewModel(ISuppliersApi api, IToastService toast, IBusyService busy, IExportService export, AuthService auth)
    {
        _api = api;
        _toast = toast;
        _busy = busy;
        _export = export;
        _auth = auth;
        Paging.Attach(LoadAsync);
        Paging.ConfigureSort([new(L["name"], "Name"), new(L["date"], "CreatedAt")]);
    }

    [RelayCommand]
    private async Task Export(string format)
    {
        try
        {
            var all = await _api.GetAllAsync();
            await _export.ExportAsync(L["suppliers"], all,
            [
                new(L["name"], s => s.Name),
                new(L["phone"], s => s.Phone),
            ], Enum.Parse<ExportFormat>(format));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    public async Task LoadAsync()
    {
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                var search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();
                var result = await _api.GetPagedAsync(Paging.Page, Paging.PageSize, Paging.SortBy, Paging.Descending, search);
                var paged = result.ToPaged();
                Suppliers.Clear();
                foreach (var s in paged.Items) Suppliers.Add(s);
                Paging.Apply(paged.Meta);
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private CancellationTokenSource? _searchCts;

    partial void OnSearchTextChanged(string value)
    {
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();
        _ = DebouncedSearchAsync(cts.Token);
    }

    private async Task DebouncedSearchAsync(CancellationToken token)
    {
        try { await Task.Delay(300, token); } catch { return; }
        if (token.IsCancellationRequested) return;
        Paging.Page = 1;
        await LoadAsync();
    }

    [RelayCommand]
    private void OpenCreate()
    {
        IsNew = true;
        _editId = 0;
        EditName = "";
        EditPhone = "";
        IsEditOpen = true;
    }

    [RelayCommand]
    private void OpenEdit(SupplierDto supplier)
    {
        IsNew = false;
        _editId = supplier.Id;
        EditName = supplier.Name;
        EditPhone = supplier.Phone ?? "";
        IsEditOpen = true;
    }

    [RelayCommand]
    private void CancelEdit() => IsEditOpen = false;

    [RelayCommand]
    private void OpenRepay(SupplierDto supplier)
    {
        RepaySupplier = supplier;
        RepayAmount = 0;
        RepayViaCard = false;
        IsRepayOpen = true;
    }

    [RelayCommand]
    private void CancelRepay() => IsRepayOpen = false;

    [RelayCommand]
    private async Task RepayAsync()
    {
        if (RepaySupplier is null || RepayAmount <= 0) { _toast.Error(L["error"]); return; }
        try
        {
            using (_busy.Begin(L["loading"]))
                await _api.PayDebtAsync(RepaySupplier.Id, new PaySupplierDebtRequest(RepayAmount, RepayViaCard));
            IsRepayOpen = false;
            _toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(EditName)) { _toast.Error(L["error"]); return; }
        var phone = string.IsNullOrWhiteSpace(EditPhone) ? null : EditPhone.Trim();
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                if (IsNew)
                    await _api.CreateAsync(new CreateSupplierRequest(EditName.Trim(), phone));
                else
                    await _api.UpdateAsync(_editId, new UpdateSupplierRequest(EditName.Trim(), phone));
            }
            IsEditOpen = false;
            _toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }
}
