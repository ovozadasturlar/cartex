namespace Cartex.Application.Common.Settings;

public sealed class ReceiptSettings
{
    public string? HeaderText { get; set; }
    public string? FooterText { get; set; }
    public int PaperWidth { get; set; } = 32;
    public string PaperFormat { get; set; } = "Thermal";
}
