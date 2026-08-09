using Cartex.Shared.Models.Printing;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IPrintingApi
{
    [Get("/api/printing/nodes")]
    Task<List<PrintNodeDto>> GetNodesAsync([Query] long branchId, CancellationToken cancellationToken = default);

    [Post("/api/printing/nodes/register")]
    Task<RegisterPrintNodeResult> RegisterNodeAsync([Body] RegisterPrintNodeRequest request, CancellationToken cancellationToken = default);

    [Post("/api/printing/nodes/heartbeat")]
    Task HeartbeatAsync([Body] PrintNodeHeartbeatRequest request, CancellationToken cancellationToken = default);

    [Put("/api/printing/nodes/{id}")]
    Task SetNodeAsync(long id, [Body] SetPrintNodeStateRequest request, CancellationToken cancellationToken = default);

    [Get("/api/printing/requesters")]
    Task<List<PrintRequesterDeviceDto>> GetRequesterDevicesAsync([Query] long branchId, CancellationToken cancellationToken = default);

    [Put("/api/printing/requesters/{id}")]
    Task SetRequesterDeviceAsync(long id, [Body] SetPrintRequesterDeviceTrustRequest request, CancellationToken cancellationToken = default);

    [Put("/api/printing/endpoints/{id}")]
    Task SetEndpointAsync(long id, [Body] SetPrinterEndpointRequest request, CancellationToken cancellationToken = default);

    [Get("/api/printing/routes")]
    Task<List<PrintRoutingPolicyDto>> GetRoutesAsync([Query] long branchId, CancellationToken cancellationToken = default);

    [Get("/api/printing/bootstrap")]
    Task<PrintingBootstrapDto> GetBootstrapAsync([Query] long branchId, [Query] string? deviceId = null, CancellationToken cancellationToken = default);

    [Put("/api/printing/routes/{kind}")]
    Task<PrintRoutingPolicyDto> SetRouteAsync(PrintJobKind kind, [Query] long branchId, [Body] UpdatePrintRoutingPolicyRequest request, CancellationToken cancellationToken = default);

    [Put("/api/printing/receipt-policy")]
    Task<PrintRoutingPolicyDto> SetReceiptPolicyAsync([Query] long branchId, [Body] UpdateReceiptPrintPolicyRequest request, CancellationToken cancellationToken = default);

    [Post("/api/printing/jobs")]
    Task<PrintJobDto> CreateJobAsync([Body] CreatePrintJobRequest request, CancellationToken cancellationToken = default);

    [Get("/api/printing/jobs")]
    Task<List<PrintJobDto>> GetJobsAsync([Query] long branchId, [Query] int take = 100, CancellationToken cancellationToken = default);

    [Get("/api/printing/jobs/assigned")]
    Task<List<AssignedPrintJobDto>> GetAssignedAsync([Query] string deviceId, [Header("X-Print-Host-Token")] string hostToken, CancellationToken cancellationToken = default);

    [Post("/api/printing/jobs/{id}/accept")]
    Task AcceptAsync(long id, [Body] PrintJobLeaseRequest request, CancellationToken cancellationToken = default);

    [Post("/api/printing/jobs/{id}/submitted")]
    Task SubmittedAsync(long id, [Body] PrintJobSubmittedRequest request, CancellationToken cancellationToken = default);

    [Post("/api/printing/jobs/{id}/complete")]
    Task CompleteAsync(long id, [Body] PrintJobLeaseRequest request, CancellationToken cancellationToken = default);

    [Post("/api/printing/jobs/{id}/fail")]
    Task FailAsync(long id, [Body] PrintJobFailedRequest request, CancellationToken cancellationToken = default);

    [Post("/api/printing/jobs/{id}/retry")]
    Task<PrintJobDto> RetryAsync(long id, CancellationToken cancellationToken = default);

    [Post("/api/printing/jobs/{id}/cancel")]
    Task CancelAsync(long id, CancellationToken cancellationToken = default);
}
