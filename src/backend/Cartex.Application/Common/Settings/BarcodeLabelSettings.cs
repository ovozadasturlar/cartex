namespace Cartex.Application.Common.Settings;

public sealed class BarcodeLabelSettings
{
    public bool DefaultWithPrice { get; set; }
    public bool AllowPriceOverride { get; set; } = true;
    public bool ShowSku { get; set; }
    public int NameLines { get; set; } = 2;
    public string CurrencyDisplay { get; set; } = "symbol";
    public string CurrencyCase { get; set; } = "original";
}
