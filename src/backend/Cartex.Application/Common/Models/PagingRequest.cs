namespace Cartex.Application.Common.Models;

public record PagingRequest : SortingRequest
{
    public const int MaxPageSize = 200;
    public const int MaxUnboundedSize = 50_000;
    public int Page { get; set; }
    public int PageSize { get; set; }
}
