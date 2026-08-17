using Cartex.Application.Branches.Commands;
using Cartex.Application.Branches.Queries;
using SetBranchCatalogVisibilityRequest = Cartex.Shared.Models.Branches.SetBranchCatalogVisibilityRequest;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cartex.Shared.Models.Branches;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BranchesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Branches.View)]
    public async Task<ActionResult<IReadOnlyCollection<BranchDto>>> GetBranches([FromQuery] GetBranchesQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(AppPermissions.Branches.Create)]
    public async Task<ActionResult<long>> CreateBranch(CreateBranchCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }

    [HttpPut("{id:long}")]
    [HasPermission(AppPermissions.Branches.Edit)]
    public async Task<IActionResult> UpdateBranch(long id, UpdateBranchCommand command)
    {
        await sender.Send(command with { Id = id });
        return NoContent();
    }

    [HttpGet("{branchId:long}/catalog")]
    [HasPermission(AppPermissions.Products.View)]
    public async Task<ActionResult<BranchCatalogPageDto>> GetCatalog(long branchId, [FromQuery] string? search = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 80)
    {
        var result = await sender.Send(new GetBranchCatalogQuery(branchId, search, page, pageSize));
        return Ok(result);
    }

    [HttpPut("{branchId:long}/catalog/{variantId:long}")]
    [HasPermission(AppPermissions.Products.Edit)]
    public async Task<IActionResult> SetCatalogVisibility(long branchId, long variantId, SetBranchCatalogVisibilityRequest request)
    {
        await sender.Send(new SetBranchCatalogVisibilityCommand(branchId, variantId, request.VisibilityOverride));
        return NoContent();
    }
}
