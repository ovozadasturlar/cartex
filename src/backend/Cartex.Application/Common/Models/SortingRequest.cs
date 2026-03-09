namespace Cartex.Application.Common.Models;

public record SortingRequest
{
    public string? SortBy { get; set; }
    public bool Descending { get; set; }
}
