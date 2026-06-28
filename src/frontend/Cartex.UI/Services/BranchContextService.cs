using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Branches;
using Cartex.Shared.Models.Warehouses;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cartex.UI.Services;

public sealed partial class BranchContextService : ObservableObject
{
    private readonly IBranchesApi _branchesApi;
    private readonly IWarehousesApi _warehousesApi;

    public ObservableCollection<BranchDto> Branches { get; } = [];
    public ObservableCollection<WarehouseDto> Warehouses { get; } = [];

    [ObservableProperty] private BranchDto? _selectedBranch;
    [ObservableProperty] private WarehouseDto? _selectedWarehouse;

    public long? CurrentBranchId => SelectedBranch?.Id;
    public long? CurrentWarehouseId => SelectedWarehouse?.Id;
    public bool HasMultipleBranches => Branches.Count > 1;

    public BranchContextService(IBranchesApi branchesApi, IWarehousesApi warehousesApi)
    {
        _branchesApi = branchesApi;
        _warehousesApi = warehousesApi;
    }

    public async Task LoadAsync()
    {
        try
        {
            var branches = await _branchesApi.GetAllAsync();
            Branches.Clear();
            foreach (var b in branches.Where(b => b.IsActive))
                Branches.Add(b);
            OnPropertyChanged(nameof(HasMultipleBranches));
            SelectedBranch = Branches.FirstOrDefault();
        }
        catch
        {
        }
    }

    partial void OnSelectedBranchChanged(BranchDto? value) => _ = LoadWarehousesAsync();

    private async Task LoadWarehousesAsync()
    {
        Warehouses.Clear();
        SelectedWarehouse = null;
        if (SelectedBranch is null) return;

        try
        {
            var warehouses = await _warehousesApi.GetAllAsync(SelectedBranch.Id);
            foreach (var w in warehouses)
                Warehouses.Add(w);
            SelectedWarehouse = Warehouses.FirstOrDefault();
        }
        catch
        {
        }
    }
}
