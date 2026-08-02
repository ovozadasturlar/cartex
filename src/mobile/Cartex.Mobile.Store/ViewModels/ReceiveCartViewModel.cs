using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.Supplies;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Store.ViewModels;

public partial class ReceiveCartViewModel : ObservableObject
{
    private readonly SupplyCartStore _cart;
    private readonly WarehouseContext _warehouse;
    private readonly ISuppliesApi _suppliesApi;

    public ObservableCollection<SupplyCartLine> Lines { get; } = [];

    [ObservableProperty] private bool _isEmpty = true;
    [ObservableProperty] private bool _isBusy;

    public ReceiveCartViewModel(SupplyCartStore cart, WarehouseContext warehouse, ISuppliesApi suppliesApi)
    {
        _cart = cart;
        _warehouse = warehouse;
        _suppliesApi = suppliesApi;
    }

    public void Appear()
    {
        _cart.Changed += Refresh;
        Refresh();
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
    }

    [RelayCommand]
    private void Increment(SupplyCartLine line) => _cart.SetQuantity(line.VariantId, line.Quantity + line.QuantityStep);

    [RelayCommand]
    private void Decrement(SupplyCartLine line)
    {
        if (line.Quantity > line.QuantityStep) _cart.SetQuantity(line.VariantId, line.Quantity - line.QuantityStep);
    }

    [RelayCommand]
    private void Remove(SupplyCartLine line) => _cart.Remove(line.VariantId);

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (IsBusy) return;
        if (_cart.Lines.Count == 0)
        {
            Ui.Toast(Loc.Instance["err_no_items"]);
            return;
        }
        if (!await _warehouse.EnsureSelectedAsync())
        {
            Ui.Toast(Loc.Instance["warehouse_none"]);
            return;
        }
        IsBusy = true;
        try
        {
            var request = new CreateSupplyRequest(
                null,
                _warehouse.WarehouseId!.Value,
                DateOnly.FromDateTime(DateTime.Today),
                _cart.Lines.Select(l => new CreateSupplyItemRequest(l.VariantId, l.Quantity, 0, null)).ToList()
            );
            await _suppliesApi.CreateAsync(request);
            _cart.Clear();
            Ui.Toast(Loc.Instance["saved_successfully"]);
            await Shell.Current.GoToAsync("..");
        }
        catch (Refit.ApiException ex)
        {
            await Shell.Current.CurrentPage.DisplayAlertAsync(Loc.Instance["error"], ApiErrors.Describe(ex), Loc.Instance["ok"]);
        }
        catch
        {
            Ui.Toast(Loc.Instance["err_no_connection"]);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
