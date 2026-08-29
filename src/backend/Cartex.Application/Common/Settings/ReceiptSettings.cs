namespace Cartex.Application.Common.Settings;

public sealed class ReceiptSettings
{
    public string? HeaderText { get; set; }
    public string? FooterText { get; set; }
    public int PaperWidth { get; set; } = 32;
    public string PaperFormat { get; set; } = "Thermal";
    public bool ShowBusinessName { get; set; } = true;
    public bool ShowBranchName { get; set; } = true;
    public bool ShowAddress { get; set; } = true;
    public bool ShowPhone { get; set; } = true;
    public bool ShowCashier { get; set; } = true;
    public bool ShowCustomer { get; set; } = true;
    public bool ShowReceiptNumber { get; set; } = true;
    public bool ShowPaymentDetails { get; set; } = true;
    public bool ShowQrCode { get; set; } = true;
    public bool ShowElectronicLink { get; set; } = true;
    public bool ShowLogo { get; set; } = true;
    public bool ShowCustomerPhone { get; set; } = true;
    public bool ShowCustomerEmail { get; set; } = true;
    public string? Language { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string? PublicReceiptBaseUrl { get; set; }
}
