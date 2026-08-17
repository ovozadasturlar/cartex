using Cartex.Application.Common.Messaging;
using Cartex.Application.Notifications.Commands;
using Cartex.Application.Notifications.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cartex.Shared.Models.Notifications;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController(ISender sender) : ControllerBase
{
    [HttpPost("providers/playmobile/status")]
    [AllowAnonymous]
    public async Task<IActionResult> PlayMobileStatus(ProcessPlayMobileStatusCommand command)
    {
        command = command with { Authorization = Request.Headers.Authorization.ToString() };
        await sender.Send(command);
        return Ok();
    }

    [HttpGet("journal")]
    [HasPermission(AppPermissions.Notifications.JournalView)]
    public async Task<ActionResult<IReadOnlyCollection<NotificationDeliveryDto>>> GetJournal(
        [FromQuery] GetNotificationJournalQuery query) =>
        Ok(await sender.Send(query));

    [HttpGet("stats")]
    [HasPermission(AppPermissions.Notifications.JournalView)]
    public async Task<ActionResult<NotificationStatsDto>> GetStats([FromQuery] GetNotificationStatsQuery query) =>
        Ok(await sender.Send(query));

    [HttpGet("options")]
    [HasPermission(AppPermissions.Notifications.JournalView)]
    public async Task<ActionResult<NotificationJournalOptionsDto>> GetOptions() =>
        Ok(await sender.Send(new GetNotificationJournalOptionsQuery()));

    [HttpPost("export-audit")]
    [HasPermission(AppPermissions.Notifications.JournalExport)]
    public async Task<IActionResult> RecordExport(RecordNotificationExportCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }
}
