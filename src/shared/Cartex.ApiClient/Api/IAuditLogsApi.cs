using Cartex.Shared.Models.AuditLogs;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IAuditLogsApi
{
    [Get("/api/audit-logs")]
    Task<List<AuditLogDto>> GetAllAsync([Query] string? tableName = null, [Query] string? userName = null,
        [Query] string? action = null, [Query] DateTime? fromDate = null, [Query] DateTime? toDate = null);

    [Get("/api/audit-logs")]
    Task<IApiResponse<List<AuditLogDto>>> GetPagedAsync([Query] int page, [Query] int pageSize,
        [Query] string? sortBy = null, [Query] bool descending = false, [Query] string? search = null,
        [Query] string? tableName = null, [Query] string? userName = null, [Query] string? action = null,
        [Query] DateTime? fromDate = null, [Query] DateTime? toDate = null);
}
