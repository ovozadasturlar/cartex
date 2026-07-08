using Cartex.Mobile.Agent.Data;
using Cartex.Mobile.Agent.Models;
using Cartex.Mobile.Agent.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Agent.ViewModels;

public partial class RepayViewModel(AgentDb db, SyncService sync) : ObservableObject, IQueryAttributable
{
    [ObservableProperty] private string _customerName = "";
    [ObservableProperty] private string _debtText = "";
    [ObservableProperty] private string _amountText = "";
    [ObservableProperty] private string? _error;

    private long _customerId;
    private LocalCustomer? _customer;
    private string _currency = "";

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("customerId", out var id))
            _customerId = (long)id;
    }

    public async Task AppearAsync()
    {
        _customer = await db.GetCustomerAsync(_customerId);
        _currency = await db.GetMetaAsync("base_currency") ?? "";
        if (_customer is null) return;
        CustomerName = _customer.FullName;
        DebtText = $"Joriy qarz: {_customer.DebtBalance:N0} {_currency} (holat: {await db.GetMetaAsync("last_sync") ?? "—"})";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        Error = null;
        if (_customer is null) return;
        if (!decimal.TryParse(AmountText?.Replace(" ", ""), out var amount) || amount <= 0)
        {
            Error = "Summani kiriting";
            return;
        }
        await sync.EnqueueRepayAsync(new RepayDraft(_customer.Id, _customer.FullName, amount));
        Ui.Toast("To'lov navbatga qo'shildi");
        await Shell.Current.GoToAsync("..");
    }
}
