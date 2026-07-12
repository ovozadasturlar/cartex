using System.Text.Json;
using Cartex.Mobile.Agent.Data;
using Cartex.Mobile.Agent.Models;
using Cartex.Mobile.Agent.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Cartex.Mobile.Core;

namespace Cartex.Mobile.Agent.ViewModels;

public partial class DaySummaryViewModel(AgentDb db) : ObservableObject
{
    [ObservableProperty] private string _dateText = "";
    [ObservableProperty] private string _cashText = "";
    [ObservableProperty] private string _salesCount = "0";
    [ObservableProperty] private string _salesTotalText = "";
    [ObservableProperty] private string _debtGivenText = "";
    [ObservableProperty] private string _debtCollectedText = "";
    [ObservableProperty] private string _deliveredCount = "0";
    [ObservableProperty] private string _vanValueText = "";
    [ObservableProperty] private string? _pendingWarning;

    public async Task AppearAsync()
    {
        var currency = await db.GetMetaAsync("base_currency") ?? "";
        var today = DateTime.Today;
        var outbox = (await db.GetOutboxAsync()).Where(o => o.CreatedAt >= today).ToList();

        decimal cash = 0, salesTotal = 0, debtGiven = 0, repaid = 0;
        var salesCount = 0;
        foreach (var item in outbox.Where(o => o.Status != "error"))
            switch (item.Kind)
            {
                case "sale":
                {
                    var d = JsonSerializer.Deserialize<SaleDraft>(item.PayloadJson)!;
                    var total = d.Items.Sum(i => i.Quantity * i.UnitPrice);
                    salesCount++;
                    salesTotal += total;
                    cash += d.PaidCash;
                    debtGiven += Math.Max(0, total - d.PaidCash);
                    break;
                }
                case "checkout":
                {
                    var c = JsonSerializer.Deserialize<CheckoutDraft>(item.PayloadJson)!;
                    salesCount++;
                    salesTotal += c.Total;
                    cash += c.PaidCash;
                    debtGiven += Math.Max(0, c.Total - c.PaidCash);
                    break;
                }
                case "repay":
                {
                    var r = JsonSerializer.Deserialize<RepayDraft>(item.PayloadJson)!;
                    cash += r.Amount;
                    repaid += r.Amount;
                    break;
                }
            }

        var delivered = (await db.GetOrdersAsync()).Count(o => o.DeliveredAt >= today);
        var stock = await db.GetVanStockAsync();

        DateText = today.ToString("dd.MM.yyyy");
        CashText = $"{cash:N0} {currency}";
        SalesCount = salesCount.ToString();
        SalesTotalText = $"{salesTotal:N0} {currency}";
        DebtGivenText = $"{debtGiven:N0} {currency}";
        DebtCollectedText = $"{repaid:N0} {currency}";
        DeliveredCount = delivered.ToString();
        VanValueText = $"{stock.Sum(s => s.Quantity * s.SellingPrice):N0} {currency}";

        var notSent = await db.CountOutboxAsync("pending") + await db.CountOutboxAsync("error");
        PendingWarning = notSent > 0 ? string.Format(Loc.Instance["summary_pending_fmt"], notSent) : null;
    }
}
