using Cartex.Application.AuditLogs.Queries;
using Cartex.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize]
public class AuditLogsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission("audit.view")]
    public async Task<IActionResult> GetAuditLogs([FromQuery] GetAuditLogsQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }
}
