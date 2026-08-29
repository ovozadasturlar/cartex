using Cartex.Shared.Models.Notifications;
using Refit;

namespace Cartex.ApiClient.Api;

public interface INotificationsApi
{
    [Get("/api/notifications/journal")]
    Task<IApiResponse<List<NotificationDeliveryDto>>> QueryAsync([Query] IDictionary<string, object> query);

    [Get("/api/notifications/journal")]
    Task<List<NotificationDeliveryDto>> GetAllAsync(
        [Query] int page = 0,
        [Query] int pageSize = 0,
        [Query] DateTime? from = null,
        [Query] DateTime? to = null,
        [Query] string? channel = null,
        [Query] string? provider = null,
        [Query] string? status = null,
        [Query] string? purpose = null);

    [Get("/api/notifications/stats")]
    Task<NotificationStatsDto> GetStatsAsync(
        [Query] DateTime? from = null,
        [Query] DateTime? to = null,
        [Query] string? channel = null,
        [Query] string? provider = null,
        [Query] string? status = null,
        [Query] string? purpose = null);

    [Get("/api/notifications/options")]
    Task<NotificationJournalOptionsDto> GetOptionsAsync();

    [Post("/api/notifications/export-audit")]
    Task RecordExportAsync([Body] RecordNotificationExportRequest request);
}
