using Cartex.Application.Common.Messaging;
using Cartex.Application.Printing;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Shared.Models.Printing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/printing")]
[Authorize]
[RequiresFeature(FeatureCatalog.RemotePrinting)]
[EnableRateLimiting("printing")]
public sealed class PrintingController(ISender sender) : ControllerBase
{
    [HttpGet("nodes")]
    [HasPermission(AppPermissions.Printing.NodesView)]
    public async Task<ActionResult<IReadOnlyList<PrintNodeDto>>> GetNodes(long branchId, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetPrintNodesQuery(branchId), cancellationToken));

    [HttpPost("nodes/register")]
    [HasPermission(AppPermissions.Printing.Host)]
    public async Task<ActionResult<RegisterPrintNodeResult>> Register(RegisterPrintNodeRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new RegisterPrintNodeCommand(request), cancellationToken));

    [HttpPost("nodes/heartbeat")]
    [HasPermission(AppPermissions.Printing.Host)]
    public async Task<IActionResult> Heartbeat(PrintNodeHeartbeatRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new HeartbeatPrintNodeCommand(request), cancellationToken);
        return NoContent();
    }

    [HttpPut("nodes/{id:long}")]
    [HasPermission(AppPermissions.Printing.NodesManage)]
    public async Task<IActionResult> SetNode(long id, SetPrintNodeStateRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new SetPrintNodeStateCommand(id, request), cancellationToken);
        return NoContent();
    }

    [HttpGet("requesters")]
    [HasPermission(AppPermissions.Printing.NodesView)]
    public async Task<ActionResult<IReadOnlyList<PrintRequesterDeviceDto>>> GetRequesters(long branchId, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetPrintRequesterDevicesQuery(branchId), cancellationToken));

    [HttpGet("devices")]
    [HasPermission(AppPermissions.Printing.NodesView)]
    public async Task<ActionResult<PrintDevicesDto>> GetDevices(long branchId, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetPrintDevicesQuery(branchId), cancellationToken));

    [HttpPut("devices/trust")]
    [HasPermission(AppPermissions.Printing.NodesManage)]
    public async Task<IActionResult> SetDeviceTrust(SetPrintDeviceTrustRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new SetPrintDeviceTrustCommand(request), cancellationToken);
        return NoContent();
    }

    [HttpPut("devices/auto-trust")]
    [HasPermission(AppPermissions.Printing.NodesManage)]
    public async Task<IActionResult> SetAutoTrust(SetPrintAutoTrustRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new SetPrintAutoTrustCommand(request), cancellationToken);
        return NoContent();
    }

    [HttpPut("requesters/{id:long}")]
    [HasPermission(AppPermissions.Printing.NodesManage)]
    public async Task<IActionResult> SetRequester(long id, SetPrintRequesterDeviceTrustRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new SetPrintRequesterDeviceTrustCommand(id, request), cancellationToken);
        return NoContent();
    }

    [HttpPut("endpoints/{id:long}")]
    [HasPermission(AppPermissions.Printing.NodesManage)]
    public async Task<IActionResult> SetEndpoint(long id, SetPrinterEndpointRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new SetPrinterEndpointCommand(id, request), cancellationToken);
        return NoContent();
    }

    [HttpGet("routes")]
    [HasPermission(AppPermissions.Printing.RoutesView)]
    public async Task<ActionResult<IReadOnlyList<PrintRoutingPolicyDto>>> GetRoutes(long branchId, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetPrintRoutingPoliciesQuery(branchId), cancellationToken));

    [HttpGet("bootstrap")]
    [HasPermission(AppPermissions.Printing.RoutesView, AppPermissions.Printing.ReceiptPrint, AppPermissions.Printing.RemoteUse)]
    public async Task<ActionResult<PrintingBootstrapDto>> GetBootstrap(
        long branchId,
        string? deviceId,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetPrintingBootstrapQuery(branchId, deviceId), cancellationToken));

    [HttpPut("routes/{kind}")]
    [HasPermission(AppPermissions.Printing.RoutesEdit)]
    public async Task<ActionResult<PrintRoutingPolicyDto>> SetRoute(long branchId, PrintJobKind kind, UpdatePrintRoutingPolicyRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new UpdatePrintRoutingPolicyCommand(branchId, kind, request), cancellationToken));

    [HttpPut("receipt-policy")]
    [HasPermission(AppPermissions.Printing.RoutesEdit)]
    public async Task<ActionResult<PrintRoutingPolicyDto>> SetReceiptPolicy(
        long branchId,
        UpdateReceiptPrintPolicyRequest request,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new UpdateReceiptPrintPolicyCommand(branchId, request), cancellationToken));

    [HttpPost("jobs")]
    [HasPermission(AppPermissions.Printing.RemoteUse)]
    public async Task<ActionResult<PrintJobDto>> CreateJob(CreatePrintJobRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreatePrintJobCommand(request), cancellationToken);
        if (result.Status == PrintJobStatus.Rejected)
            return StatusCode(StatusCodes.Status403Forbidden, result);
        return CreatedAtAction(nameof(GetJobs), new { branchId = result.BranchId }, result);
    }

    [HttpGet("jobs")]
    [HasPermission(AppPermissions.Printing.JobsViewOwn, AppPermissions.Printing.JobsViewBranch)]
    public async Task<ActionResult<IReadOnlyList<PrintJobDto>>> GetJobs(long branchId, int take = 100, CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new GetPrintJobsQuery(branchId, take), cancellationToken));

    [HttpGet("jobs/assigned")]
    [HasPermission(AppPermissions.Printing.Host)]
    public async Task<ActionResult<IReadOnlyList<AssignedPrintJobDto>>> GetAssigned(
        string deviceId,
        [FromHeader(Name = "X-Print-Host-Token")] string hostToken,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetAssignedPrintJobsQuery(deviceId, hostToken), cancellationToken));

    [HttpPost("jobs/{id:long}/accept")]
    [HasPermission(AppPermissions.Printing.Host)]
    public async Task<IActionResult> Accept(long id, PrintJobLeaseRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new AcceptPrintJobCommand(id, request), cancellationToken);
        return NoContent();
    }

    [HttpPost("jobs/{id:long}/submitted")]
    [HasPermission(AppPermissions.Printing.Host)]
    public async Task<IActionResult> Submitted(long id, PrintJobSubmittedRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new SubmitPrintJobCommand(id, request), cancellationToken);
        return NoContent();
    }

    [HttpPost("jobs/{id:long}/complete")]
    [HasPermission(AppPermissions.Printing.Host)]
    public async Task<IActionResult> Complete(long id, PrintJobLeaseRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new CompletePrintJobCommand(id, request), cancellationToken);
        return NoContent();
    }

    [HttpPost("jobs/{id:long}/fail")]
    [HasPermission(AppPermissions.Printing.Host)]
    public async Task<IActionResult> Fail(long id, PrintJobFailedRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new FailPrintJobCommand(id, request), cancellationToken);
        return NoContent();
    }

    [HttpPost("jobs/{id:long}/retry")]
    [HasPermission(AppPermissions.Printing.JobsRetry)]
    public async Task<ActionResult<PrintJobDto>> Retry(long id, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new RetryPrintJobCommand(id), cancellationToken));

    [HttpPost("jobs/{id:long}/cancel")]
    [HasPermission(AppPermissions.Printing.JobsCancel)]
    public async Task<IActionResult> Cancel(long id, CancellationToken cancellationToken)
    {
        await sender.Send(new CancelPrintJobCommand(id), cancellationToken);
        return NoContent();
    }
}
