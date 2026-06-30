using Cartex.Shared.Models.Shifts;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IShiftsApi
{
    [Get("/api/shifts/current")]
    Task<CurrentShiftDto?> GetCurrentAsync();

    [Get("/api/shifts")]
    Task<IApiResponse<List<ShiftHistoryDto>>> GetHistoryAsync([Query] int page, [Query] int pageSize);

    [Get("/api/shifts/{id}/report")]
    Task<ZReportDto> GetReportAsync(long id);

    [Post("/api/shifts/open")]
    Task<long> OpenAsync([Body] OpenShiftRequest request);

    [Post("/api/shifts/{id}/close")]
    Task<ZReportDto> CloseAsync(long id, [Body] CloseShiftRequest request);

    [Post("/api/shifts/cash-movement")]
    Task CashMovementAsync([Body] CashMovementRequest request);
}
