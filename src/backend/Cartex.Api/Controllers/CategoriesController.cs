using Cartex.Application.Categories.Commands;
using Cartex.Application.Categories.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cartex.Shared.Models.Categories;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CategoriesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Categories.View)]
    public async Task<ActionResult<IReadOnlyCollection<CategoryDto>>> GetCategories([FromQuery] GetCategoriesQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(AppPermissions.Categories.Create)]
    public async Task<ActionResult<long>> CreateCategory(CreateCategoryCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }

    [HttpPut("{id:long}")]
    [HasPermission(AppPermissions.Categories.Edit)]
    public async Task<IActionResult> UpdateCategory(long id, UpdateCategoryCommand command)
    {
        await sender.Send(command with { Id = id });
        return NoContent();
    }

    [HttpPut("{id:long}/move")]
    [HasPermission(AppPermissions.Categories.Edit)]
    public async Task<IActionResult> MoveCategory(long id, MoveCategoryRequest request)
    {
        await sender.Send(new MoveCategoryCommand(id, request.ParentId, request.SortOrder));
        return NoContent();
    }

    [HttpPost("{id:long}/merge")]
    [HasPermission(AppPermissions.Categories.Edit)]
    public async Task<ActionResult<int>> MergeCategory(long id, MergeCategoryRequest request)
    {
        return Ok(await sender.Send(new MergeCategoryCommand(id, request.TargetId)));
    }
}
