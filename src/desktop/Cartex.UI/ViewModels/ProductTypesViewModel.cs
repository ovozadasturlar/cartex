using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Products;
using Cartex.UI.Models;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class ProductTypesViewModel : ViewModelBase, ILoadable
{
    private readonly IProductTypesApi _api;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly IExportService _export;
    private readonly AuthService _auth;
    private List<ProductTypeDto> _all = [];
    private long _editId;

    public ObservableCollection<ProductTypeDto> ProductTypes { get; } = [];
    public ObservableCollection<AttributeFieldRow> SchemaFields { get; } = [];
    public IReadOnlyList<string> FieldTypes { get; } = ["text", "number", "select", "bool"];

    [ObservableProperty] private bool _isEditOpen;
    [ObservableProperty] private bool _isNew;
    [ObservableProperty] private string _editName = "";
    [ObservableProperty] private bool _editTracksExpiry;
    [ObservableProperty] private string? _searchText;

    public bool IsEmpty => ProductTypes.Count == 0;
    public bool CanExport => _auth.HasPermission("reports.export");
    public bool CanCreate => _auth.HasPermission("product_types.create");
    public bool CanEdit => _auth.HasPermission("product_types.edit");

    private IReadOnlyList<PageShortcut>? _shortcuts;

    public IReadOnlyList<PageShortcut> Shortcuts => _shortcuts ??=
        CrudShortcuts(OpenCreateCommand, SaveCommand, () => IsEditOpen = false, () => IsEditOpen);

    public ProductTypesViewModel(IProductTypesApi api, IToastService toast, IBusyService busy, IExportService export, AuthService auth)
    {
        _api = api;
        _toast = toast;
        _busy = busy;
        _export = export;
        _auth = auth;
    }

    public async Task LoadAsync()
    {
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                _all = (await _api.GetAllAsync()).ToList();
                ApplyFilter();
            }
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    partial void OnSearchTextChanged(string? value) => ApplyFilter();

    private void ApplyFilter()
    {
        var q = SearchText?.Trim();
        IEnumerable<ProductTypeDto> items = _all;
        if (!string.IsNullOrEmpty(q))
            items = items.Where(t => t.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
        ProductTypes.Clear();
        foreach (var t in items) ProductTypes.Add(t);
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private async Task Export(string format)
    {
        try
        {
            // Use the already-filtered ProductTypes collection (respects SearchText local filter)
            var items = ProductTypes.ToList();
            await _export.ExportAsync(L["product_types"], items,
            [
                new(L["name"], t => t.Name),
            ], Enum.Parse<ExportFormat>(format));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void OpenCreate()
    {
        if (!CanCreate) return;
        IsNew = true;
        _editId = 0;
        EditName = "";
        EditTracksExpiry = false;
        SchemaFields.Clear();
        IsEditOpen = true;
    }

    [RelayCommand]
    private void OpenEdit(ProductTypeDto type)
    {
        if (!CanEdit) return;
        IsNew = false;
        _editId = type.Id;
        EditName = type.Name;
        EditTracksExpiry = type.TracksExpiry;
        SchemaFields.Clear();
        foreach (var f in AttributeSchemaCodec.ParseSchema(type.AttributeSchema)) SchemaFields.Add(f);
        IsEditOpen = true;
    }

    [RelayCommand]
    private void AddField() => SchemaFields.Add(new AttributeFieldRow());

    [RelayCommand]
    private void RemoveField(AttributeFieldRow row) => SchemaFields.Remove(row);

    [RelayCommand]
    private void CancelEdit() => IsEditOpen = false;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsNew ? !CanCreate : !CanEdit) return;
        if (string.IsNullOrWhiteSpace(EditName)) { _toast.Error(L["error"]); return; }
        var schema = AttributeSchemaCodec.SerializeSchema(SchemaFields);
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                if (IsNew)
                    await _api.CreateAsync(new CreateProductTypeRequest(EditName.Trim(), EditTracksExpiry, schema));
                else
                    await _api.UpdateAsync(_editId, new UpdateProductTypeRequest(EditName.Trim(), EditTracksExpiry, schema));
            }
            IsEditOpen = false;
            ServiceLocator.Resolve<ReferenceCache>().Invalidate(CacheKeys.ProductTypes);
            _toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }
}
