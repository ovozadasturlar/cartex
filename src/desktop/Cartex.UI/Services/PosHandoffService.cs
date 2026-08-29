namespace Cartex.UI.Services;

public sealed class PosHandoffService
{
    public string? PendingCartCode { get; set; }

    /// <summary>Sale whose cart must be reloaded into the POS after a correction.</summary>
    public long? PendingCorrectionSaleId { get; set; }
}
