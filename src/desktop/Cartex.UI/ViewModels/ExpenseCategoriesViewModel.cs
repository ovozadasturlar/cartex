using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.ExpenseCategories;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class ExpenseCategoriesViewModel : ViewModelBase, ILoadable
{
    private readonly IExpenseCategoriesApi _api;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private long _editId;

    public ObservableCollection<ExpenseCategoryDto> Categories { get; } = [];

    [ObservableProperty] private bool _isEditOpen;
    [ObservableProperty] private bool _isNew;
    [ObservableProperty] private string _editName = "";

    public bool IsEmpty => Categories.Count == 0;

    public ExpenseCategoriesViewModel(IExpenseCategoriesApi api, IToastService toast, IBusyService busy)
    {
        _api = api;
        _toast = toast;
        _busy = busy;
    }

    public async Task LoadAsync()
    {
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                var items = await _api.GetAllAsync();
                Categories.Clear();
                foreach (var c in items) Categories.Add(c);
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void OpenCreate()
    {
        IsNew = true;
        _editId = 0;
        EditName = "";
        IsEditOpen = true;
    }

    [RelayCommand]
    private void OpenEdit(ExpenseCategoryDto category)
    {
        IsNew = false;
        _editId = category.Id;
        EditName = category.Name;
        IsEditOpen = true;
    }

    [RelayCommand]
    private void CancelEdit() => IsEditOpen = false;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(EditName)) { _toast.Error(L["error"]); return; }
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                if (IsNew)
                    await _api.CreateAsync(new CreateExpenseCategoryRequest(EditName.Trim()));
                else
                    await _api.UpdateAsync(_editId, new UpdateExpenseCategoryRequest(EditName.Trim()));
            }
            IsEditOpen = false;
            _toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }
}
