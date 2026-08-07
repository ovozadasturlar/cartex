using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Branches;
using Cartex.Shared.Models.Warehouses;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cartex.UI.Services;

public sealed partial class BranchContextService : ObservableObject
{
    private readonly ISessionsApi _sessionsApi;

    public ObservableCollection<BranchDto> Branches { get; } = [];
    public ObservableCollection<WarehouseDto> Warehouses { get; } = [];

    [ObservableProperty] private BranchDto? _selectedBranch;
    [ObservableProperty] private WarehouseDto? _selectedWarehouse;

    public long? CurrentBranchId => SelectedBranch?.Id;
    public long? CurrentWarehouseId => SelectedWarehouse?.Id;
    public bool HasMultipleBranches => Branches.Count > 1;
    public bool HasMultipleWarehouses =>
        Warehouses.Count > 1;

    public BranchContextService(ISessionsApi sessionsApi) => _sessionsApi = sessionsApi;

    public async Task LoadAsync()
    {
        try
        {
            var context = await _sessionsApi.GetContextAsync();
            Branches.Clear();
            foreach (var b in context.Branches.Where(b => b.IsActive))
                Branches.Add(new BranchDto(b.Id, b.Name, null, null, b.IsActive));
            Warehouses.Clear();
            foreach (var w in context.Warehouses)
                Warehouses.Add(new WarehouseDto(w.Id, w.Name, w.BranchId, w.BranchName));
            OnPropertyChanged(nameof(HasMultipleBranches));
            SelectedBranch = Branches.FirstOrDefault(b => b.Id == context.DefaultBranchId) ?? Branches.FirstOrDefault();
            SelectedWarehouse = Warehouses.FirstOrDefault(w => w.BranchId == SelectedBranch?.Id);
            OnPropertyChanged(nameof(HasMultipleWarehouses));
        }
        catch
        {
        }
    }

    partial void OnSelectedBranchChanged(BranchDto? value)
    {
        SelectedWarehouse = Warehouses.FirstOrDefault(w => w.BranchId == value?.Id);
        OnPropertyChanged(nameof(HasMultipleWarehouses));
    }
}
