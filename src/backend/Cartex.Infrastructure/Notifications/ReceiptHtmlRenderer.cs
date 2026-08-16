using System.Net;
using System.Text;
using Cartex.Application.Common.Settings;
using Cartex.Application.Sales.Queries;
using Cartex.Shared.Localization;

namespace Cartex.Infrastructure.Notifications;

public static class ReceiptHtmlRenderer
{
    public static string Render(ReceiptDto r, ReceiptSettings? opts = null)
    {
        var sb = new StringBuilder();
        string E(string? s) => WebUtility.HtmlEncode(s ?? "");
        string T(string key) => ReceiptTexts.Get(key, r.Language);
        sb.Append("<!doctype html><html lang=\"uz\"><head><meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.Append($"<title>{E(r.BusinessName)} — chek</title>");
        sb.Append("<style>");
        sb.Append(":root{color-scheme:light dark}");
        sb.Append("*{box-sizing:border-box}");
        sb.Append("body{font-family:-apple-system,system-ui,'Segoe UI',Roboto,sans-serif;background:#eef2f7;margin:0;padding:20px 14px 34px}");
        sb.Append(".card{max-width:400px;margin:0 auto;background:#fff;border-radius:20px;overflow:hidden;box-shadow:0 8px 30px rgba(15,23,42,.10)}");
        sb.Append(".head{background:linear-gradient(135deg,#14532d,#16a34a);color:#fff;padding:24px 22px 20px;text-align:center}");
        sb.Append(".logo{width:52px;height:52px;border-radius:12px;background:rgba(255,255,255,.16);display:flex;align-items:center;justify-content:center;margin:0 auto 10px;font-size:22px;object-fit:contain}");
        sb.Append("h1{font-size:19px;margin:0;letter-spacing:.2px}");
        sb.Append(".sub{opacity:.85;font-size:12.5px;margin:3px 0 0}");
        sb.Append(".meta{display:flex;justify-content:center;gap:8px;padding:12px 18px 0;flex-wrap:wrap}");
        sb.Append(".chip{background:#f1f5f9;color:#475569;border-radius:999px;padding:4px 12px;font-size:11.5px}");
        sb.Append(".body{padding:8px 22px 22px}");
        sb.Append(".item{display:flex;justify-content:space-between;gap:12px;padding:10px 0;border-bottom:1px solid #f1f5f9}");
        sb.Append(".item:last-of-type{border-bottom:none}");
        sb.Append(".iname{font-weight:600;font-size:13.5px;color:#0f172a}");
        sb.Append(".iqty{color:#94a3b8;font-size:12px;margin-top:2px}");
        sb.Append(".isum{font-weight:600;font-size:13.5px;color:#0f172a;white-space:nowrap}");
        sb.Append(".totals{background:#f0fdf4;border-radius:14px;padding:14px 16px;margin:14px 0 4px}");
        sb.Append(".trow{display:flex;justify-content:space-between;font-size:13px;color:#475569;margin:4px 0}");
        sb.Append(".grand{font-size:19px;font-weight:800;color:#14532d;margin:2px 0}");
        sb.Append(".grand span:last-child{white-space:nowrap}");
        sb.Append(".pays{margin-top:10px}");
        sb.Append(".prow{display:flex;justify-content:space-between;font-size:13px;margin:5px 0;color:#334155}");
        sb.Append(".prow .lbl{color:#94a3b8}");
        sb.Append(".approx{color:#94a3b8;font-size:11px}");
        sb.Append(".debt{color:#dc2626;font-weight:600}");
        sb.Append(".plus{color:#16a34a;font-weight:600}");
        sb.Append(".foot{border-top:1px dashed #cbd5e1;margin-top:16px;padding-top:14px;text-align:center}");
        sb.Append(".thanks{font-size:13px;color:#475569;margin:0 0 10px}");
        sb.Append(".pdf{display:block;background:#166534;color:#fff;text-decoration:none;text-align:center;border-radius:12px;padding:12px;font-size:14px;font-weight:600}");
        sb.Append("@media(prefers-color-scheme:dark){body{background:#0f172a}.card{background:#1e293b;box-shadow:none}");
        sb.Append(".iname,.isum{color:#e2e8f0}.chip{background:#334155;color:#cbd5e1}.item{border-color:#334155}");
        sb.Append(".totals{background:#14261d}.grand{color:#4ade80}.trow{color:#94a3b8}.prow{color:#cbd5e1}.approx{color:#64748b}");
        sb.Append(".foot{border-color:#475569}.thanks{color:#94a3b8}}");
        sb.Append("</style></head><body>");

        sb.Append("<div class=\"card\"><div class=\"head\">");
        if (opts?.ShowLogo != false && !string.IsNullOrWhiteSpace(r.LogoImageKey))
            sb.Append($"<img class=\"logo\" alt=\"logo\" src=\"/api/storage/content?key={Uri.EscapeDataString(r.LogoImageKey)}\">");
        else if (opts?.ShowLogo != false)
            sb.Append("<div class=\"logo\">🧾</div>");
        if (opts?.ShowBusinessName != false) sb.Append($"<h1>{E(r.BusinessName)}</h1>");
        if (opts?.ShowBranchName != false) sb.Append($"<p class=\"sub\">{E(r.BranchName)}</p>");
        if (opts?.ShowAddress != false && !string.IsNullOrEmpty(r.BranchAddress)) sb.Append($"<p class=\"sub\">{E(r.BranchAddress)}</p>");
        if (opts?.ShowPhone != false && !string.IsNullOrEmpty(r.BranchPhone)) sb.Append($"<p class=\"sub\">{E(r.BranchPhone)}</p>");
        if (!string.IsNullOrWhiteSpace(opts?.HeaderText)) sb.Append($"<p class=\"sub\">{E(opts.HeaderText)}</p>");
        sb.Append("</div>");

        sb.Append("<div class=\"meta\">");
        sb.Append($"<span class=\"chip\">{r.SaleDate.ToLocalTime():dd.MM.yyyy HH:mm}</span>");
        if (opts?.ShowReceiptNumber != false) sb.Append($"<span class=\"chip\">{E(T("receipt_no"))} {r.SaleId}</span>");
        if (opts?.ShowCashier != false) sb.Append($"<span class=\"chip\">{E(r.UserName)}</span>");
        if (opts?.ShowCustomer != false && !string.IsNullOrWhiteSpace(r.CustomerName)) sb.Append($"<span class=\"chip\">{E(r.CustomerName)}</span>");
        if (opts?.ShowCustomerPhone != false && !string.IsNullOrWhiteSpace(r.CustomerPhone)) sb.Append($"<span class=\"chip\">{E(r.CustomerPhone)}</span>");
        if (opts?.ShowCustomerEmail == true && !string.IsNullOrWhiteSpace(r.CustomerEmail)) sb.Append($"<span class=\"chip\">{E(r.CustomerEmail)}</span>");
        sb.Append("</div>");

        sb.Append("<div class=\"body\">");
        foreach (var item in r.Items)
        {
            sb.Append("<div class=\"item\"><div>");
            sb.Append($"<div class=\"iname\">{E(item.ProductName)}</div>");
            sb.Append($"<div class=\"iqty\">{item.Quantity:0.###} {E(item.UnitName)} × {item.UnitPrice:N0}</div>");
            if (item.DiscountAmount > 0)
                sb.Append($"<div class=\"iqty\">{T("discount")} −{item.DiscountAmount:N0}</div>");
            sb.Append($"</div><div class=\"isum\">{item.LineTotal - item.DiscountAmount:N0}</div></div>");
        }

        sb.Append("<div class=\"totals\">");
        if (r.DiscountAmount > 0) sb.Append($"<div class=\"trow\"><span>{T("discount")}</span><span>−{r.DiscountAmount:N0}</span></div>");
        sb.Append($"<div class=\"trow grand\"><span>{T("total")}</span><span>{r.TotalAmount:N0}</span></div>");
        sb.Append("</div>");

        if (opts?.ShowPaymentDetails != false)
        {
            sb.Append("<div class=\"pays\">");
            if (r.Payments.Count > 0)
                foreach (var payment in r.Payments)
                    Row(sb, E(ReceiptTexts.PaymentLabel(payment.Method, r.Language)),
                        payment.IsForeign ? $"{payment.Amount:N2} {E(payment.Currency)} <span class=\"approx\">≈ {payment.AmountBase:N0}</span>" : $"{payment.Amount:N0}");
            else
            {
                if (r.PaidCash > 0) Row(sb, T("cash"), $"{r.PaidCash:N0}");
                if (r.PaidCard > 0) Row(sb, T("card"), $"{r.PaidCard:N0}");
                if (r.PaidBonus > 0) Row(sb, T("bonus"), $"{r.PaidBonus:N0}");
            }
            if (r.PaidAdvance > 0) Row(sb, T("advance"), $"{r.PaidAdvance:N0}");
            if (r.ChangeAmount > 0) Row(sb, T("change"), $"{r.ChangeAmount:N0}");
            if (r.CreditAmount > 0) Row(sb, T("credit"), $"{r.CreditAmount:N0}");
            if (r.DebtAmount > 0) Row(sb, T("debt"), $"{r.DebtAmount:N0}", "debt");
            if (r.CashbackEarned > 0) Row(sb, "Cashback", $"+{r.CashbackEarned:N0}", "plus");
            sb.Append("</div>");
        }

        sb.Append("<div class=\"foot\">");
        sb.Append($"<p class=\"thanks\">{E(string.IsNullOrWhiteSpace(opts?.FooterText) ? T("thanks") : opts.FooterText)}</p>");
        sb.Append($"<a class=\"pdf\" href=\"/r/{E(r.ReceiptToken)}/pdf\">{T("download_pdf")}</a>");
        sb.Append("</div></div></div></body></html>");
        return sb.ToString();

        static void Row(StringBuilder sb, string label, string value, string? cls = null) =>
            sb.Append($"<div class=\"prow\"><span class=\"lbl\">{label}</span><span{(cls is null ? "" : $" class=\"{cls}\"")}>{value}</span></div>");
    }
}
