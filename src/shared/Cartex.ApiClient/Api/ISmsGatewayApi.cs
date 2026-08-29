using Cartex.Shared.Models.SmsGateway;
using Refit;

namespace Cartex.ApiClient.Api;

public interface ISmsGatewayApi
{
    [Get("/api/sms-gateway/devices")]
    Task<IReadOnlyList<SmsGatewayDeviceDto>> GetDevicesAsync(long branchId, CancellationToken cancellationToken = default);

    [Post("/api/sms-gateway/devices/register")]
    Task<RegisterSmsGatewayResult> RegisterAsync([Body] RegisterSmsGatewayRequest request, CancellationToken cancellationToken = default);

    [Post("/api/sms-gateway/devices/heartbeat")]
    Task HeartbeatAsync([Body] SmsGatewayHeartbeatRequest request, CancellationToken cancellationToken = default);

    [Put("/api/sms-gateway/devices/{id}")]
    Task UpdateAsync(long id, [Body] UpdateSmsGatewayDeviceRequest request, CancellationToken cancellationToken = default);

    [Put("/api/sms-gateway/devices/{id}/trust")]
    Task SetTrustAsync(long id, [Body] SetSmsGatewayTrustRequest request, CancellationToken cancellationToken = default);

    [Put("/api/sms-gateway/devices/{id}/consent")]
    Task SetConsentAsync(long id, [Body] SetSmsGatewayConsentRequest request, CancellationToken cancellationToken = default);

    [Put("/api/sms-gateway/devices/{id}/consent-settings")]
    Task<UpdateSmsGatewayConsentResult> UpdateConsentAsync(long id, [Body] UpdateSmsGatewayConsentRequest request, CancellationToken cancellationToken = default);

    [Put("/api/sms-gateway/devices/{id}/pause")]
    Task SetPauseAsync(long id, [Body] SetSmsGatewayPauseRequest request, CancellationToken cancellationToken = default);

    [Post("/api/sms-gateway/host-state")]
    Task<SmsGatewayHostStateDto> GetHostStateAsync([Body] SmsGatewayHostStateRequest request, CancellationToken cancellationToken = default);

    [Get("/api/sms-gateway/jobs")]
    Task<IReadOnlyList<SmsGatewayJobDto>> GetJobsAsync(long branchId, int take = 100, CancellationToken cancellationToken = default);

    [Get("/api/sms-gateway/journal")]
    Task<SmsGatewayJournalDto> GetJournalAsync(long branchId, string? status = null, string? kind = null,
        long? deviceId = null, long? customerId = null, DateTime? from = null, DateTime? to = null,
        int take = 100, CancellationToken cancellationToken = default);

    [Post("/api/sms-gateway/jobs/retry")]
    Task RetryAsync([Body] SmsGatewayJobIdsRequest request, CancellationToken cancellationToken = default);

    [Post("/api/sms-gateway/jobs/cancel")]
    Task CancelAsync([Body] SmsGatewayJobIdsRequest request, CancellationToken cancellationToken = default);

    [Post("/api/sms-gateway/jobs/reassign")]
    Task ReassignAsync([Body] ReassignSmsGatewayJobsRequest request, CancellationToken cancellationToken = default);

    [Post("/api/sms-gateway/jobs/assigned")]
    Task<IReadOnlyList<AssignedSmsGatewayJobDto>> GetAssignedAsync([Body] SmsGatewayHostRequest request, CancellationToken cancellationToken = default);

    [Post("/api/sms-gateway/jobs/{id}/sent")]
    Task MarkSentAsync(long id, [Body] SmsGatewayLeaseRequest request, CancellationToken cancellationToken = default);

    [Post("/api/sms-gateway/jobs/{id}/delivered")]
    Task MarkDeliveredAsync(long id, [Body] SmsGatewayLeaseRequest request, CancellationToken cancellationToken = default);

    [Post("/api/sms-gateway/jobs/{id}/simulated")]
    Task MarkSimulatedAsync(long id, [Body] SmsGatewayLeaseRequest request, CancellationToken cancellationToken = default);

    [Post("/api/sms-gateway/jobs/test")]
    Task<SmsGatewayJobDto?> SendTestAsync([Body] SendSmsGatewayTestRequest request, CancellationToken cancellationToken = default);

    [Post("/api/sms-gateway/jobs/{id}/failed")]
    Task MarkFailedAsync(long id, [Body] SmsGatewayFailureRequest request, CancellationToken cancellationToken = default);
}
