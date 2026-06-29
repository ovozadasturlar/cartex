namespace Cartex.Domain.Common;

public interface IFeatureStateProvider
{
    Task<bool> IsEnabledAsync(string code, CancellationToken cancellationToken = default);
    void Invalidate();
}
