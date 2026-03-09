using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Customers;

namespace Cartex.UI.ViewModels;

public partial class CustomersViewModel : ViewModelBase
{
    private readonly ICustomersApi _customersApi;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private CustomerDto? _selectedCustomer;

    [ObservableProperty]
    private string _newFullName = string.Empty;

    [ObservableProperty]
    private string _newPhone = string.Empty;

    [ObservableProperty]
    private bool _isAddingNew;

    public ObservableCollection<CustomerDto> Customers { get; } = [];

    public CustomersViewModel(ICustomersApi customersApi)
    {
        _customersApi = customersApi;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText;
            var customers = await _customersApi.GetAllAsync(search);
            Customers.Clear();
            foreach (var c in customers)
                Customers.Add(c);
        }
        catch { }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ToggleAddNew() => IsAddingNew = !IsAddingNew;

    [RelayCommand]
    private async Task SaveNewAsync()
    {
        if (string.IsNullOrWhiteSpace(NewFullName)) return;
        try
        {
            await _customersApi.CreateAsync(new CreateCustomerRequest(NewFullName, NewPhone, null, 0));
            NewFullName = string.Empty;
            NewPhone = string.Empty;
            IsAddingNew = false;
            await LoadAsync();
        }
        catch { }
    }

    partial void OnSearchTextChanged(string value) => _ = LoadAsync();
}
