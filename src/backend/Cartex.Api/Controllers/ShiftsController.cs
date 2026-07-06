using Cartex.Application.Shifts.Commands;
using Cartex.Application.Shifts.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ShiftsController(ISender sender) : ControllerBase
{
    [HttpGet("current")]
    [HasPermission(AppPermissions.Shifts.Manage)]
    public async Task<IActionResult> GetCurrent()
    {
        var result = await sender.Send(new GetCurrentShiftQuery());
        return result is null ? NoContent() : Ok(result);
    }

    [HttpGet]
    [HasPermission(AppPermissions.Shifts.View)]
    public async Task<IActionResult> GetHistory([FromQuery] GetShiftsQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpGet("{id:long}/report")]
    [HasPermission(AppPermissions.Shifts.View)]
    public async Task<IActionResult> GetReport(long id)
    {
        var result = await sender.Send(new GetShiftReportQuery(id));
        return Ok(result);
    }

    [HttpPost("open")]
    [HasPermission(AppPermissions.Shifts.Manage)]
    public async Task<IActionResult> Open(OpenShiftCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }

    [HttpPost("{id:long}/close")]
    [HasPermission(AppPermissions.Shifts.Manage)]
    public async Task<IActionResult> Close(long id, CloseShiftCommand command)
    {
        var report = await sender.Send(command with { ShiftId = id });
        return Ok(report);
    }

    [HttpPost("cash-movement")]
    [HasPermission(AppPermissions.Sales.CashOut)]
    public async Task<IActionResult> CashMovement(AddCashMovementCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }
}
