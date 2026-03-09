using Cartex.Shared.Models.AuditLogs;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IAuditLogsApi
{
    [Get("/api/audit-logs")]
    Task<List<AuditLogDto>> GetAllAsync([Query] string? tableName = null, [Query] DateTime? fromDate = null, [Query] DateTime? toDate = null);
}
