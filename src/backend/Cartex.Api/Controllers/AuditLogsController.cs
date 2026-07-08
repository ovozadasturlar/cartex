using Cartex.Application.AuditLogs.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize]
[RequiresFeature(FeatureCatalog.Audit)]
public class AuditLogsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Audit.View)]
    public async Task<ActionResult<IReadOnlyCollection<AuditLogDto>>> GetAuditLogs([FromQuery] GetAuditLogsQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }
}
