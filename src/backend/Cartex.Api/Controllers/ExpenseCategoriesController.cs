using Cartex.Application.ExpenseCategories.Commands;
using Cartex.Application.ExpenseCategories.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cartex.Shared.Models.ExpenseCategories;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/expense-categories")]
[Authorize]
public class ExpenseCategoriesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.ExpenseCategories.View)]
    public async Task<ActionResult<IReadOnlyCollection<ExpenseCategoryDto>>> GetExpenseCategories()
    {
        var result = await sender.Send(new GetExpenseCategoriesQuery());
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(AppPermissions.ExpenseCategories.Create)]
    public async Task<ActionResult<long>> CreateExpenseCategory(CreateExpenseCategoryCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }

    [HttpPut("{id:long}")]
    [HasPermission(AppPermissions.ExpenseCategories.Edit)]
    public async Task<IActionResult> UpdateExpenseCategory(long id, UpdateExpenseCategoryCommand command)
    {
        await sender.Send(command with { Id = id });
        return NoContent();
    }
}
