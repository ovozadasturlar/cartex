using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;

namespace Cartex.Application.Tests.Common;

public sealed class TestProductReferenceSource : IProductReferenceSource
{
    public IReadOnlyList<ProductReferenceRow> Rows { get; set; } = [];
    public int FetchCount { get; private set; }

    public Task<IReadOnlyList<ProductReferenceRow>> FetchAsync(ProductReferenceSourceConfig config, CancellationToken cancellationToken)
    {
        FetchCount++;
        return Task.FromResult(Rows);
    }

    public void Reset()
    {
        Rows = [];
        FetchCount = 0;
    }
}
