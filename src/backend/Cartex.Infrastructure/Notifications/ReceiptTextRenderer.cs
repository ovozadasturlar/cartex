using Cartex.Application.Common.Settings;
using Cartex.Shared.Models.Printing;
using Cartex.Shared.Models.Sales;

namespace Cartex.Infrastructure.Notifications;

public static class ReceiptTextRenderer
{
    public static string Render(ReceiptDto receipt, ReceiptSettings? options, string template = "auto") =>
        ReceiptTextFormatter.Format(receipt, new ReceiptTextOptions(
            options?.HeaderText,
            options?.FooterText,
            ReceiptPaper.Sanitize(options?.PaperWidth ?? 0),
            options?.ShowBusinessName != false,
            options?.ShowBranchName != false,
            options?.ShowAddress != false,
            options?.ShowPhone != false,
            options?.ShowCashier != false,
            options?.ShowCustomer != false,
            options?.ShowReceiptNumber != false,
            options?.ShowPaymentDetails != false,
            options?.ShowQrCode != false,
            options?.ShowElectronicLink != false,
            options?.PublicReceiptBaseUrl,
            options?.ShowLogo != false,
            options?.ShowCustomerPhone != false,
            options?.ShowCustomerEmail == true,
            template)).Text;
}
