using System.Text;
using Cartex.Application.Common.Settings;
using Cartex.Application.Sales.Queries;
using Cartex.Shared.Localization;

namespace Cartex.Infrastructure.Notifications;

public static class ReceiptTextRenderer
{
    public static string Render(ReceiptDto r, ReceiptSettings? opts)
    {
        string T(string key) => ReceiptTexts.Get(key, r.Language);
        var sb = new StringBuilder();
        sb.AppendLine(r.BusinessName);
        if (!string.IsNullOrWhiteSpace(r.BranchName)) sb.AppendLine(r.BranchName);
        if (!string.IsNullOrWhiteSpace(opts?.HeaderText)) sb.AppendLine(opts.HeaderText);
        sb.AppendLine(r.SaleDate.ToLocalTime().ToString("dd.MM.yyyy HH:mm"));
        sb.AppendLine("————————————");
        foreach (var i in r.Items)
        {
            sb.AppendLine(i.ProductName);
            sb.AppendLine($"  {i.Quantity:0.###} x {i.UnitPrice:N0} = {i.LineTotal:N0}");
        }
        sb.AppendLine("————————————");
        if (r.DiscountAmount > 0) sb.AppendLine($"{T("discount")}: {r.DiscountAmount:N0}");
        sb.AppendLine($"{T("total")}: {r.TotalAmount:N0}");
        if (r.Payments.Count > 0)
            foreach (var p in r.Payments)
                sb.AppendLine($"{ReceiptTexts.PaymentLabel(p.Method, r.Language)}: {(p.IsForeign ? $"{p.Amount:N2} {p.Currency} ≈ {p.AmountBase:N0}" : $"{p.Amount:N0}")}");
        else
        {
            if (r.PaidCash > 0) sb.AppendLine($"{T("cash")}: {r.PaidCash:N0}");
            if (r.PaidCard > 0) sb.AppendLine($"{T("card")}: {r.PaidCard:N0}");
            if (r.PaidBonus > 0) sb.AppendLine($"{T("bonus")}: {r.PaidBonus:N0}");
        }
        if (r.ChangeAmount > 0) sb.AppendLine($"{T("change")}: {r.ChangeAmount:N0}");
        if (r.CreditAmount > 0) sb.AppendLine($"{T("credit")}: {r.CreditAmount:N0}");
        if (r.DebtAmount > 0) sb.AppendLine($"{T("debt")}: {r.DebtAmount:N0}");
        if (r.CashbackEarned > 0) sb.AppendLine($"{T("cashback")}: {r.CashbackEarned:N0}");
        sb.AppendLine(T(r.DebtAmount > 0 ? "unpaid" : "paid"));
        sb.Append(string.IsNullOrWhiteSpace(opts?.FooterText) ? T("thanks") : opts.FooterText);
        return sb.ToString();
    }
}
