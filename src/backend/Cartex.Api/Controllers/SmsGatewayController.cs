using Cartex.Application.Common.Messaging;
using Cartex.Application.Sms;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Shared.Models.SmsGateway;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/sms-gateway")]
[Authorize]
public sealed class SmsGatewayController(ISender sender) : ControllerBase
{
    [HttpGet("devices")]
    [HasPermission(AppPermissions.SmsGateway.Edit)]
    public async Task<ActionResult<IReadOnlyList<SmsGatewayDeviceDto>>> Devices(long branchId, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetSmsGatewayDevicesQuery(branchId), cancellationToken));

    [HttpPost("devices/register")]
    [HasPermission(AppPermissions.SmsGateway.Host)]
    public async Task<ActionResult<RegisterSmsGatewayResult>> Register(RegisterSmsGatewayRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new RegisterSmsGatewayCommand(request), cancellationToken));

    [HttpPost("devices/heartbeat")]
    [HasPermission(AppPermissions.SmsGateway.Host)]
    public async Task<IActionResult> Heartbeat(SmsGatewayHeartbeatRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new HeartbeatSmsGatewayCommand(request), cancellationToken);
        return NoContent();
    }

    [HttpPut("devices/{id:long}")]
    [HasPermission(AppPermissions.SmsGateway.Edit)]
    public async Task<IActionResult> Update(long id, UpdateSmsGatewayDeviceRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new UpdateSmsGatewayDeviceCommand(id, request), cancellationToken);
        return NoContent();
    }

    [HttpPut("devices/{id:long}/trust")]
    [HasPermission(AppPermissions.SmsGateway.Edit)]
    public async Task<IActionResult> Trust(long id, SetSmsGatewayTrustRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new SetSmsGatewayTrustCommand(id, request), cancellationToken);
        return NoContent();
    }

    [HttpPut("devices/{id:long}/consent")]
    [HasPermission(AppPermissions.SmsGateway.Host)]
    public async Task<IActionResult> Consent(long id, SetSmsGatewayConsentRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new SetSmsGatewayConsentCommand(id, request), cancellationToken);
        return NoContent();
    }

    [HttpPut("devices/{id:long}/consent-settings")]
    [HasPermission(AppPermissions.SmsGateway.Host)]
    public async Task<ActionResult<UpdateSmsGatewayConsentResult>> ConsentSettings(
        long id, UpdateSmsGatewayConsentRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new UpdateSmsGatewayConsentCommand(id, request), cancellationToken));

    [HttpPut("devices/{id:long}/pause")]
    [HasPermission(AppPermissions.SmsGateway.Host)]
    public async Task<IActionResult> Pause(long id, SetSmsGatewayPauseRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new SetSmsGatewayPauseCommand(id, request), cancellationToken);
        return NoContent();
    }

    [HttpPost("host-state")]
    [HasPermission(AppPermissions.SmsGateway.Host)]
    public async Task<ActionResult<SmsGatewayHostStateDto>> HostState(
        SmsGatewayHostStateRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetSmsGatewayHostStateQuery(request), cancellationToken));

    [HttpGet("jobs")]
    [HasPermission(AppPermissions.SmsGateway.Edit)]
    public async Task<ActionResult<IReadOnlyList<SmsGatewayJobDto>>> Jobs(long branchId, int take, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetSmsGatewayJobsQuery(branchId, take == 0 ? 100 : take), cancellationToken));

    [HttpGet("journal")]
    [HasPermission(AppPermissions.SmsGateway.Edit)]
    public async Task<ActionResult<SmsGatewayJournalDto>> Journal(
        long branchId, string? status, string? kind, long? deviceId, long? customerId,
        DateTime? from, DateTime? to, int take, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetSmsGatewayJournalQuery(branchId, status, kind, deviceId, customerId,
            from, to, take == 0 ? 100 : take), cancellationToken));

    [HttpPost("jobs/retry")]
    [HasPermission(AppPermissions.SmsGateway.Edit)]
    public async Task<IActionResult> Retry(SmsGatewayJobIdsRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new RetrySmsGatewayJobsCommand(request.JobIds), cancellationToken);
        return NoContent();
    }

    [HttpPost("jobs/cancel")]
    [HasPermission(AppPermissions.SmsGateway.Edit)]
    public async Task<IActionResult> Cancel(SmsGatewayJobIdsRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new CancelSmsGatewayJobsCommand(request.JobIds), cancellationToken);
        return NoContent();
    }

    [HttpPost("jobs/reassign")]
    [HasPermission(AppPermissions.SmsGateway.Edit)]
    public async Task<IActionResult> Reassign(ReassignSmsGatewayJobsRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new ReassignSmsGatewayJobsCommand(request.JobIds, request.DeviceId), cancellationToken);
        return NoContent();
    }

    [HttpPost("jobs")]
    [HasPermission(AppPermissions.SmsGateway.Edit)]
    public async Task<ActionResult<SmsGatewayJobDto?>> Create(CreateSmsGatewayJobRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new CreateSmsGatewayJobCommand(request), cancellationToken));

    [HttpPost("jobs/assigned")]
    [HasPermission(AppPermissions.SmsGateway.Host)]
    public async Task<ActionResult<IReadOnlyList<AssignedSmsGatewayJobDto>>> Assigned(SmsGatewayHostRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetAssignedSmsGatewayJobsQuery(request.DeviceId, request.SimSlot, request.HostToken), cancellationToken));

    [HttpPost("jobs/{id:long}/sent")]
    [HasPermission(AppPermissions.SmsGateway.Host)]
    public async Task<IActionResult> Sent(long id, SmsGatewayLeaseRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new MarkSmsGatewayJobSentCommand(id, request), cancellationToken);
        return NoContent();
    }

    [HttpPost("jobs/{id:long}/delivered")]
    [HasPermission(AppPermissions.SmsGateway.Host)]
    public async Task<IActionResult> Delivered(long id, SmsGatewayLeaseRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new MarkSmsGatewayJobDeliveredCommand(id, request), cancellationToken);
        return NoContent();
    }

    [HttpPost("jobs/{id:long}/simulated")]
    [HasPermission(AppPermissions.SmsGateway.Host)]
    public async Task<IActionResult> Simulated(long id, SmsGatewayLeaseRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new MarkSmsGatewayJobSimulatedCommand(id, request), cancellationToken);
        return NoContent();
    }

    [HttpPost("jobs/test")]
    [HasPermission(AppPermissions.SmsGateway.Host)]
    public async Task<ActionResult<SmsGatewayJobDto?>> Test(SendSmsGatewayTestRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new SendSmsGatewayTestCommand(request), cancellationToken));

    [HttpPost("jobs/{id:long}/failed")]
    [HasPermission(AppPermissions.SmsGateway.Host)]
    public async Task<IActionResult> Failed(long id, SmsGatewayFailureRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new FailSmsGatewayJobCommand(id, request), cancellationToken);
        return NoContent();
    }
}
