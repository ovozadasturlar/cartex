namespace Cartex.Application.Common.Settings;

public sealed class ProformaSettings
{
    public string? HeaderText { get; set; }
    public string? FooterText { get; set; }
    public int PaperWidth { get; set; } = 32;
    public string PaperFormat { get; set; } = "Thermal";
    public bool ShowBusinessName { get; set; } = true;
    public bool ShowAddress { get; set; } = true;
    public bool ShowPhone { get; set; } = true;
    public bool ShowSeller { get; set; } = true;
    public bool ShowCustomer { get; set; } = true;
    public bool ShowNote { get; set; } = true;
    public bool ShowCartCode { get; set; } = true;
}
