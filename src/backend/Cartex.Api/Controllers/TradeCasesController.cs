using Cartex.Application.Common.Messaging;
using Cartex.Application.Sales.Commands;
using Cartex.Application.TradeCases.Commands;
using Cartex.Application.TradeCases.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Enums;
using Cartex.Shared.Models.TradeCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cartex.Application.Common.Participants;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/trade-cases")]
[Authorize]
[RequiresFeature(FeatureCatalog.TradeCases)]
public sealed class TradeCasesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.TradeCases.View)]
    public async Task<ActionResult<IReadOnlyCollection<TradeCaseListDto>>> Get([FromQuery] GetTradeCasesQuery query) =>
        Ok(await sender.Send(query));

    [HttpGet("{id:long}")]
    [HasPermission(AppPermissions.TradeCases.View)]
    public async Task<ActionResult<TradeCaseDetailDto>> GetById(long id) =>
        Ok(await sender.Send(new GetTradeCaseByIdQuery(id)));

    [HttpPost]
    [HasPermission(AppPermissions.TradeCases.Create)]
    public async Task<ActionResult<TradeCaseCreatedDto>> Create(CreateTradeCaseRequest request) =>
        Ok(await sender.Send(new CreateTradeCaseCommand(
            request.CustomerId,
            request.WarehouseId,
            request.Title,
            request.SiteAddress,
            ParseOptional<TradeCaseWorkflow>(request.Workflow, "workflow"),
            ParseOptional<TradeCasePricePolicy>(request.PricePolicy, "pricePolicy"),
            request.Currency,
            request.BusinessDate,
            request.Note,
            request.IdempotencyKey,
            request.Participants?.Select(x => new ParticipantInput(x.RoleDefinitionId, x.PartyId)).ToList())));

    [HttpPut("{id:long}")]
    [HasPermission(AppPermissions.TradeCases.Edit)]
    public async Task<IActionResult> Update(long id, UpdateTradeCaseRequest request)
    {
        await sender.Send(new UpdateTradeCaseCommand(id, request.Title, request.SiteAddress,
            request.Note,
            request.Participants?.Select(x => new ParticipantInput(x.RoleDefinitionId, x.PartyId)).ToList(),
            request.ExpectedVersion));
        return NoContent();
    }

    [HttpPost("{id:long}/close")]
    [HasPermission(AppPermissions.TradeCases.Close)]
    public async Task<IActionResult> Close(long id, ChangeTradeCaseStatusRequest request)
    {
        await sender.Send(new ChangeTradeCaseStatusCommand(id, TradeCaseStatus.Settled,
            request.Reason, request.ExpectedVersion));
        return NoContent();
    }

    [HttpPost("{id:long}/cancel")]
    [HasPermission(AppPermissions.TradeCases.Close)]
    public async Task<IActionResult> Cancel(long id, ChangeTradeCaseStatusRequest request)
    {
        await sender.Send(new ChangeTradeCaseStatusCommand(id, TradeCaseStatus.Cancelled,
            request.Reason, request.ExpectedVersion));
        return NoContent();
    }

    [HttpPut("{id:long}/sales/{saleId:long}")]
    [HasPermission(AppPermissions.TradeCases.Edit)]
    public async Task<IActionResult> LinkSale(long id, long saleId)
    {
        await sender.Send(new LinkSaleToTradeCaseCommand(id, saleId));
        return NoContent();
    }

    [HttpDelete("{id:long}/sales/{saleId:long}")]
    [HasPermission(AppPermissions.TradeCases.Edit)]
    public async Task<IActionResult> UnlinkSale(long id, long saleId)
    {
        await sender.Send(new UnlinkSaleFromTradeCaseCommand(id, saleId));
        return NoContent();
    }

    [HttpPost("{id:long}/issues")]
    [HasPermission(AppPermissions.GoodsIssues.Create)]
    public async Task<ActionResult<GoodsIssueCreatedDto>> Issue(long id, CreateGoodsIssueRequest request) =>
        Ok(await sender.Send(new CreateGoodsIssueCommand(id,
            request.Lines.Select(x => new GoodsIssueLineInput(x.VariantId, x.Quantity, x.UnitPrice)).ToList(),
            request.BusinessDate, request.Note, request.IdempotencyKey, request.ExpectedCaseVersion)));

    [HttpGet("issues/{issueId:long}/print")]
    [HasPermission(AppPermissions.GoodsIssues.View)]
    public async Task<ActionResult<GoodsIssuePrintDto>> IssuePrint(long issueId) =>
        Ok(await sender.Send(new GetGoodsIssuePrintQuery(issueId)));

    [HttpPost("{id:long}/returns")]
    [HasPermission(AppPermissions.GoodsIssues.Return)]
    public async Task<ActionResult<GoodsReturnCreatedDto>> Return(long id, CreateGoodsReturnRequest request) =>
        Ok(await sender.Send(new CreateGoodsReturnCommand(id,
            request.Lines.Select(x => new GoodsReturnLineInput(
                x.GoodsIssueLineId, x.Quantity, x.Reason,
                Parse<ReturnItemCondition>(x.Condition, "condition"),
                Parse<InventoryDisposition>(x.Disposition, "disposition"))).ToList(),
            request.BusinessDate, request.Note, request.IdempotencyKey, request.ExpectedCaseVersion)));

    [HttpPost("{id:long}/settlements")]
    [HasPermission(AppPermissions.TradeCases.Settle)]
    public async Task<ActionResult<TradeCaseSettlementCreatedDto>> Settle(long id, SettleTradeCaseRequest request) =>
        Ok(await sender.Send(new SettleTradeCaseCommand(
            id,
            request.PaidCash,
            request.PaidCard,
            request.PaidBonus,
            request.Payments?.Select(x => new SalePaymentDto(
                Parse<PaymentMethod>(x.Method, "payment method"), x.Currency, x.Amount)).ToList(),
            request.Lines?.Select(x => new TradeCaseSettlementLineInput(x.GoodsIssueLineId, x.Quantity)).ToList(),
            request.DiscountAmount,
            request.DebtCurrency,
            request.DebtDueDate,
            request.ApplyAutoDiscount,
            request.UseCustomerAdvance,
            request.CloseWhenEmpty,
            request.BusinessDate,
            request.Note,
            request.IdempotencyKey,
            request.ExpectedCaseVersion)));

    [HttpGet("{id:long}/statement")]
    [HasPermission(AppPermissions.Statements.View)]
    public async Task<ActionResult<TradeCaseStatementDto>> Statement(
        long id, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null) =>
        Ok(await sender.Send(new GetTradeCaseStatementQuery(id, from, to)));

    [HttpGet("{id:long}/statement/export")]
    [HasPermission(AppPermissions.Statements.Export)]
    public async Task<IActionResult> ExportStatement(
        long id,
        [FromQuery] string format = "pdf",
        [FromQuery] string mode = "both",
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var file = await sender.Send(new ExportTradeCaseStatementCommand(id, format, mode, from, to));
        return File(file.Content, file.ContentType, file.FileName);
    }

    private static T Parse<T>(string value, string field) where T : struct, Enum =>
        Enum.TryParse<T>(value, true, out var result)
            ? result
            : throw new BusinessRuleException($"{field} qiymati noto'g'ri: {value}", "invalid_enum_value");

    private static T? ParseOptional<T>(string? value, string field) where T : struct, Enum =>
        string.IsNullOrWhiteSpace(value) ? null : Parse<T>(value, field);
}
