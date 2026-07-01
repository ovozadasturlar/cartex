namespace Cartex.UI.Services;

public interface IScaleService
{
    Task<decimal?> ReadWeightAsync(CancellationToken ct = default);
}
