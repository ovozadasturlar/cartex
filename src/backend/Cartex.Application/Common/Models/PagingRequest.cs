namespace Cartex.Application.Common.Models;

public record PagingRequest : SortingRequest
{
    public int Page { get; set; }
    public int PageSize { get; set; }
}
