using Cartex.Application.Sales.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("r")]
[AllowAnonymous]
public class ReceiptController(ISender sender) : ControllerBase
{
    [HttpGet("{token}")]
    public async Task<IActionResult> GetReceipt(string token)
    {
        var receipt = await sender.Send(new GetReceiptByTokenQuery(token));
        return receipt is null ? NotFound() : Ok(receipt);
    }
}
