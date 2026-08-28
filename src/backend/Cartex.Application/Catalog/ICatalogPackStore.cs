using Cartex.Shared.Models.Catalog;

namespace Cartex.Application.Catalog;

public sealed record CatalogPackState(CatalogPackDto? Pack, string? Error);

public interface ICatalogPackStore
{
    Task<CatalogPackState> StateAsync(CancellationToken cancellationToken = default);

    Task<CatalogPackDto> SaveAsync(Stream pack, Stream manifest, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(CancellationToken cancellationToken = default);
}
