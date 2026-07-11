using System.Text.Json;
using Cartex.Shared.Models.Common;
using Refit;

namespace Cartex.ApiClient.Paging;

public static class PagedResponseExtensions
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static PagedResult<T> ToPaged<T>(this IApiResponse<List<T>> response)
    {
        var items = response.Content ?? [];
        if (response.Headers is { } headers && headers.TryGetValues("X-Paging", out var values))
        {
            var meta = JsonSerializer.Deserialize<PagedListMetadata>(values.First(), Options);
            if (meta is not null) return new PagedResult<T>(items, meta);
        }
        return PagedResult<T>.Single(items);
    }
}
