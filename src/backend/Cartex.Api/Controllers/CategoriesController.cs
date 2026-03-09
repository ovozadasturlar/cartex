using Cartex.Application.Categories.Commands;
using Cartex.Application.Categories.Queries;
using Cartex.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CategoriesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission("categories.view")]
    public async Task<IActionResult> GetCategories([FromQuery] GetCategoriesQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("categories.manage")]
    public async Task<IActionResult> CreateCategory(CreateCategoryCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }
}
