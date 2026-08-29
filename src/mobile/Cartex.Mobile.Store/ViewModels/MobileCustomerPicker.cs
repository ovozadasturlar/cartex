using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Querying;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.Customers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Store.ViewModels;

// Mijoz tanlash savatda ham, to'lovda ham kerak. Mantiq bitta joyda: qidiruv, tanlash,
// bekor qilish va yangi mijoz yaratish - ikki sahifa shu komponentni ulaydi.
public sealed partial class MobileCustomerPicker(
    ICustomersApi api,
    MobileOfflineService offline,
    AccessState access,
    Action<long?, string?> apply) : ObservableObject
{
    private CancellationTokenSource? _searchCts;

    public ObservableCollection<CustomerDto> Results { get; } = [];
    public ObservableCollection<string> OpeningKinds { get; } =
        [Loc.Instance["opening_kind_debt"], Loc.Instance["opening_kind_credit"]];

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private bool _hasResults;
    [ObservableProperty] private string _customerName = "";
    [ObservableProperty] private bool _hasCustomer;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isFormOpen;

    [ObservableProperty] private string _newName = "";
    [ObservableProperty] private string _newPhone = "";
    [ObservableProperty] private string _newLastName = "";
    [ObservableProperty] private string _newAddress = "";
    [ObservableProperty] private string _newEmail = "";
    [ObservableProperty] private string _newCard = "";
    [ObservableProperty] private string _newDiscount = "";
    [ObservableProperty] private string _newCreditLimit = "";
    [ObservableProperty] private string _newOpeningBalance = "";
    [ObservableProperty] private int _newOpeningKindIndex;

    public bool CanCreate => access.CanCreateCustomer;
    public bool CanEnterOpeningBalance => access.CanEnterOpeningBalance;

    public void Sync(long? customerId, string? customerName)
    {
        CustomerName = customerName ?? "";
        HasCustomer = customerId is not null;
    }

    partial void OnSearchChanged(string value) => _ = SearchAsync(value);

    private async Task SearchAsync(string term)
    {
        var cts = Debounce.Restart(ref _searchCts);
        term = term.Trim();
        if (string.IsNullOrWhiteSpace(term))
        {
            Results.Clear();
            HasResults = false;
            return;
        }
        try
        {
            await Task.Delay(350, cts.Token);
            IReadOnlyList<CustomerDto> rows;
            if (offline.ShouldUseOffline)
                rows = await offline.SearchCustomersAsync(term, 10);
            else
            {
                var response = await api.QueryAsync(QueryRequest.Create().Page(1, 10).Search(term).Build());
                rows = response.Content ?? [];
            }
            if (cts.IsCancellationRequested) return;
            Fill(rows);
        }
        catch (OperationCanceledException) { }
        catch
        {
            offline.MarkServerUnavailable();
            if (!offline.IsEnabled || cts.IsCancellationRequested) return;
            Fill(await offline.SearchCustomersAsync(term, 10));
        }
    }

    private void Fill(IReadOnlyList<CustomerDto> rows)
    {
        Results.Clear();
        foreach (var customer in rows) Results.Add(customer);
        HasResults = Results.Count > 0;
    }

    [RelayCommand]
    private void Pick(CustomerDto customer)
    {
        apply(customer.Id, customer.FullName);
        Sync(customer.Id, customer.FullName);
        Search = "";
        Results.Clear();
        HasResults = false;
    }

    [RelayCommand]
    private void Clear()
    {
        apply(null, null);
        Sync(null, null);
    }

    [RelayCommand]
    private void OpenForm()
    {
        NewName = Search.Trim();
        NewPhone = NewLastName = NewAddress = NewEmail = NewCard = "";
        NewDiscount = NewCreditLimit = NewOpeningBalance = "";
        NewOpeningKindIndex = 0;
        IsFormOpen = true;
    }

    [RelayCommand]
    private void CloseForm() => IsFormOpen = false;

    // Faqat ism va telefon majburiy - qolgani desktopdagi kabi ixtiyoriy.
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(NewName) || string.IsNullOrWhiteSpace(NewPhone))
        {
            Ui.Toast(Loc.Instance["err_fill_all"]);
            return;
        }
        if (offline.ShouldUseOffline)
        {
            Ui.Toast(Loc.Instance["offline_mutation_blocked"]);
            return;
        }
        IsBusy = true;
        try
        {
            var opening = Number(NewOpeningBalance);
            var request = new CreateCustomerRequest(
                NewName.Trim(),
                NewPhone.Trim(),
                Text(NewCard),
                Number(NewDiscount),
                Text(NewEmail),
                Text(NewLastName),
                Text(NewAddress),
                string.IsNullOrWhiteSpace(NewCreditLimit) ? null : Number(NewCreditLimit),
                OpeningBalance: NewOpeningKindIndex == 1 ? -opening : opening);
            var id = await api.CreateAsync(request);
            apply(id, NewName.Trim());
            Sync(id, NewName.Trim());
            IsFormOpen = false;
            Search = "";
            Results.Clear();
            HasResults = false;
        }
        catch (Refit.ApiException exception) { Ui.Toast(ApiErrors.Describe(exception)); }
        catch { Ui.Toast(Loc.Instance["err_no_connection"]); }
        finally { IsBusy = false; }
    }

    private static string? Text(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static decimal Number(string value) =>
        decimal.TryParse(value.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;
}
