using Cartex.Application.Common.Messaging;
using Cartex.Application.Store.Commands;
using Cartex.Application.Store.Queries;
using Cartex.Auth.Authorization;
using Cartex.Auth.Settings;
using Cartex.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/store")]
[Authorize(AuthenticationSchemes = JwtSettings.CustomerScheme)]
[RequiresFeature(FeatureCatalog.Ordering)]
public class StoreController(ISender sender) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<StoreProfileDto>> Me() =>
        Ok(await sender.Send(new GetStoreProfileQuery()));

    [HttpPost("me/language")]
    public async Task<IActionResult> SetLanguage(SetStoreLanguageCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }

    [HttpGet("info")]
    public async Task<ActionResult<StoreInfoDto>> Info() =>
        Ok(await sender.Send(new GetStoreInfoQuery()));

    [HttpGet("catalog")]
    public async Task<ActionResult<IReadOnlyList<StoreCatalogItemDto>>> Catalog([FromQuery] GetStoreCatalogQuery query) =>
        Ok(await sender.Send(query));

    [HttpPost("carts")]
    public async Task<ActionResult<string>> SubmitCart([FromBody] SubmitStoreCartCommand command, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey) =>
        Ok(await sender.Send(command with { IdempotencyKey = command.IdempotencyKey ?? idempotencyKey }));

    [HttpGet("orders")]
    public async Task<ActionResult<IReadOnlyList<StoreOrderDto>>> Orders() =>
        Ok(await sender.Send(new GetStoreOrdersQuery()));

    [HttpGet("receipts")]
    public async Task<ActionResult<IReadOnlyList<StoreReceiptDto>>> Receipts() =>
        Ok(await sender.Send(new GetStoreReceiptsQuery()));

    [HttpGet("balance")]
    public async Task<ActionResult<StoreBalanceDto>> Balance() =>
        Ok(await sender.Send(new GetStoreBalanceQuery()));
}
