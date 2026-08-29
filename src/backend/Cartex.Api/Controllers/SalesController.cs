using Cartex.Application.Notifications;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Sales.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Shared.Models.Sales;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SalesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Sales.View)]
    public async Task<ActionResult<IReadOnlyCollection<SaleDto>>> GetSales([FromQuery] GetSalesQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpGet("list")]
    [HasPermission(AppPermissions.Sales.View)]
    public async Task<ActionResult<IReadOnlyCollection<SaleListDto>>> GetSaleList([FromQuery] GetSaleListQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpGet("totals")]
    [HasPermission(AppPermissions.Sales.View)]
    public async Task<ActionResult<SalesTotalsDto>> GetTotals([FromQuery] GetSalesTotalsQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpGet("totals/daily")]
    [HasPermission(AppPermissions.Sales.View)]
    public async Task<ActionResult<IReadOnlyCollection<DailySalesPointDto>>> GetDailyTotals([FromQuery] GetDailySalesQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    [HasPermission(AppPermissions.Sales.View)]
    public async Task<ActionResult<SaleDetailDto>> GetById(long id) =>
        Ok(await sender.Send(new GetSaleByIdQuery(id)));

    [HttpPost]
    [HasPermission(AppPermissions.Sales.Checkout)]
    public async Task<ActionResult<CreateSaleResult>> CreateSale(CreateSaleCommand command)
    {
        var result = await sender.Send(command);
        return Ok(result);
    }

    [HttpPost("{id:long}/resend-receipt")]
    [HasPermission(AppPermissions.Customers.Message)]
    public async Task<IActionResult> ResendReceipt(long id)
    {
        await sender.Send(new ResendReceiptCommand(id));
        return NoContent();
    }

    [HttpPost("{id:long}/receipt-sms")]
    [HasPermission(AppPermissions.Customers.Message)]
    public async Task<ActionResult<ReceiptSmsResultDto>> SendReceiptSms(long id, SendReceiptSmsRequest request) =>
        Ok(await sender.Send(new SendReceiptSmsCommand(id, request.ConfirmationToken)));

    [HttpGet("{id:long}/receipt-sms-preview")]
    [HasPermission(AppPermissions.Customers.Message)]
    public async Task<ActionResult<ReceiptSmsPreviewDto>> GetReceiptSmsPreview(long id) =>
        Ok(await sender.Send(new GetReceiptSmsPreviewQuery(id)));

    [HttpGet("variant-prices/{variantId:long}")]
    [HasPermission(AppPermissions.Sales.View)]
    public async Task<ActionResult<IReadOnlyCollection<VariantSalePriceDto>>> VariantPrices(
        long variantId,
        [FromQuery] long? customerId = null,
        [FromQuery] int take = 10) =>
        Ok(await sender.Send(new GetVariantSalePricesQuery(variantId, customerId, take)));

    [HttpPost("{id:long}/void")]
    [HasPermission(AppPermissions.Sales.Void)]
    public async Task<IActionResult> VoidSale(long id, [FromBody] VoidSaleRequest request)
    {
        await sender.Send(new VoidSaleCommand(id, request.Reason));
        return NoContent();
    }

    [HttpPut("{id:long}/customer/{customerId:long}")]
    [HasPermission(AppPermissions.Sales.AssignCustomer)]
    public async Task<IActionResult> AssignCustomer(long id, long customerId)
    {
        await sender.Send(new AssignCustomerToSaleCommand(id, customerId));
        return NoContent();
    }
}
