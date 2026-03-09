namespace Cartex.Shared.Models.Common;

public record ApiResponse<T>(bool Success, T? Data, string? Message = null, List<string>? Errors = null);
