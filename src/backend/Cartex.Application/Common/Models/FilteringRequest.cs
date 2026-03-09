namespace Cartex.Application.Common.Models;

public record FilteringRequest : PagingRequest
{
    public Dictionary<string, List<string>>? Filters { get; set; }
    public string? Search { get; set; }
    public double? TimeZone { get; set; }
}
