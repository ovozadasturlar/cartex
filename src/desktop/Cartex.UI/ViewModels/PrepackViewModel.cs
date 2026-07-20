using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Prepacks;
using Cartex.Shared.Models.Stocks;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class PrepackViewModel(
    IPrepacksApi api,
    IStocksApi stocksApi,
    IBarcodeLabelService labels,
    IToastService toast,
    IBusyService busy) : ViewModelBase
{
    private long _warehouseId;

    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private StockOnHandDto? _selected;
    [ObservableProperty] private decimal _quantity = 1;
    [ObservableProperty] private int _count = 1;
    [ObservableProperty] private int? _expiresHours;

    public ObservableCollection<StockOnHandDto> Results { get; } = [];
    public ObservableCollection<PrepackDto> Active { get; } = [];

    public async Task OpenAsync(long warehouseId)
    {
        _warehouseId = warehouseId;
        SearchText = string.Empty;
        Selected = null;
        Quantity = 1;
        Count = 1;
        ExpiresHours = null;
        Results.Clear();
        IsOpen = true;
        await RefreshActiveAsync();
    }

    [RelayCommand]
    private void Close() => IsOpen = false;

    [RelayCommand]
    private async Task Search()
    {
        try
        {
            var page = await stocksApi.GetOnHandAsync(_warehouseId, null,
                string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(), 1, 20, forSale: true);
            Results.Clear();
            foreach (var s in page.Items) Results.Add(s);
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task Create()
    {
        if (Selected is null) { toast.Warning(L["product"]); return; }
        if (Quantity <= 0) { toast.Warning(L["quantity"]); return; }

        try
        {
            using (busy.Begin(L["loading"]))
            {
                var created = await api.CreateAsync(new CreatePrepacksRequest(_warehouseId, Selected.VariantId, Quantity, Count, ExpiresHours));
                foreach (var label in created)
                    labels.PrintLabels(label.LabelCode, $"{label.ProductName} {label.Quantity:0.###} {label.UnitName} = {label.Price:N0}", 1, null);
                toast.Success(L["prepack_printed"]);
                await RefreshActiveAsync();
            }
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task CancelPrepack(PrepackDto prepack)
    {
        try
        {
            await api.CancelAsync(prepack.Id);
            await RefreshActiveAsync();
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }

    private async Task RefreshActiveAsync()
    {
        try
        {
            var items = await api.GetAllAsync(_warehouseId);
            Active.Clear();
            foreach (var p in items) Active.Add(p);
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }
}
