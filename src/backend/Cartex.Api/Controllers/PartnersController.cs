using Cartex.Application.Partners.Commands;
using Cartex.Application.Partners.Queries;
using Cartex.Application.Common.Messaging;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Enums;
using Cartex.Shared.Models.Partners;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/partners")]
[Authorize]
[RequiresFeature(FeatureCatalog.Partners)]
public sealed class PartnersController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Partners.View)]
    public async Task<ActionResult<IReadOnlyCollection<PartnerDto>>> Get([FromQuery] GetPartnersQuery query) =>
        Ok(await sender.Send(query));

    [HttpPost]
    [HasPermission(AppPermissions.Partners.Edit)]
    public async Task<ActionResult<long>> Create(CreatePartnerRequest request) =>
        Ok(await sender.Send(new CreatePartnerCommand(request.FullName, request.Phone, request.Email,
            request.Address, request.CustomerId, request.Note)));

    [HttpPut("{id:long}")]
    [HasPermission(AppPermissions.Partners.Edit)]
    public async Task<IActionResult> Update(long id, UpdatePartnerRequest request)
    {
        await sender.Send(new UpdatePartnerCommand(id, request.FullName, request.Phone,
            request.Email, request.Address, request.IsEnabled, request.Note));
        return NoContent();
    }

    [HttpGet("customers/{customerId:long}")]
    [HasPermission(AppPermissions.Partners.View)]
    public async Task<ActionResult<CustomerPartnerDto?>> ForCustomer(long customerId) =>
        Ok(await sender.Send(new GetCustomerPartnerQuery(customerId)));

    [HttpPut("customers/{customerId:long}")]
    [HasPermission(AppPermissions.Partners.Edit)]
    public async Task<ActionResult<CustomerPartnerDto?>> SetForCustomer(
        long customerId,
        SetCustomerPartnershipRequest request) =>
        Ok(await sender.Send(new SetCustomerPartnershipCommand(customerId, request.IsPartner)));

    [HttpPut("{id:long}/publicity")]
    [HasPermission(AppPermissions.Partners.Publish)]
    public async Task<IActionResult> SetPublicity(long id, SetPartnerPublicityRequest request)
    {
        if (!Enum.TryParse<Cartex.Domain.Enums.PublicConsentState>(request.Consent, true, out var consent))
            return BadRequest();
        await sender.Send(new SetPartnerPublicityCommand(id, consent, request.PublicVisible,
            request.PublicPhoneVisible, request.PublicDisplayName, request.PublicAbout));
        return NoContent();
    }

    [HttpGet("{partnerId:long}/rewards")]
    [HasPermission(AppPermissions.PartnerRewards.View)]
    public async Task<ActionResult<IReadOnlyCollection<PartnerRewardEntryDto>>> Rewards(
        long partnerId,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50) =>
        Ok(await sender.Send(new GetPartnerRewardEntriesQuery(partnerId)
            { Search = search, Page = page, PageSize = pageSize }));

    [HttpGet("roles")]
    [HasPermission(AppPermissions.Partners.View)]
    public async Task<ActionResult<IReadOnlyCollection<ParticipantRoleDto>>> Roles(
        [FromQuery] bool includeDisabled = false) =>
        Ok(await sender.Send(new GetParticipantRolesQuery(includeDisabled)));

    [HttpPut("roles")]
    [HasPermission(AppPermissions.Partners.ConfigureRoles)]
    public async Task<ActionResult<long>> SaveRole(SaveParticipantRoleRequest request) =>
        Ok(await sender.Send(new SaveParticipantRoleCommand(request.Id, request.Key,
            request.SingularLabel, request.PluralLabel, request.IsEnabled, request.IsRequired,
            request.CanEqualBuyer, request.MaxCount, request.AppliesToCart, request.AppliesToSale,
            request.SortOrder)));

    [HttpGet("programs")]
    [HasPermission(AppPermissions.PartnerRewards.View)]
    public async Task<ActionResult<IReadOnlyCollection<PartnerProgramDto>>> Programs(
        [FromQuery] bool includeDisabled = false) =>
        Ok(await sender.Send(new GetPartnerProgramsQuery(includeDisabled)));

    [HttpPut("programs")]
    [HasPermission(AppPermissions.PartnerRewards.Configure)]
    public async Task<ActionResult<long>> SaveProgram(SavePartnerProgramRequest request) =>
        Ok(await sender.Send(new SavePartnerProgramCommand(request.Id, request.RoleDefinitionId,
            request.Name, request.IsEnabled, Parse<PartnerRewardMode>(request.Mode, "mode"),
            Parse<PartnerRewardBasis>(request.Basis, "basis"),
            Parse<PartnerRewardTrigger>(request.Trigger, "trigger"), request.Value,
            request.BranchId, request.CapPerSale, request.HoldDays,
            request.Rules?.Select(x => new PartnerRewardRuleInput(
                Parse<CashbackScope>(x.Scope, "scope"), x.TargetId,
                x.IsExcluded, x.ValueOverride, x.Priority)).ToList())));

    [HttpGet("ranking")]
    [HasPermission(AppPermissions.PartnerRewards.View)]
    public async Task<ActionResult<IReadOnlyCollection<PartnerRankingDto>>> Ranking(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int take = 100) =>
        Ok(await sender.Send(new GetPartnerRankingQuery(from, to, take)));

    [HttpPost("{partnerId:long}/redemptions")]
    [HasPermission(AppPermissions.PartnerRewards.Redeem)]
    public async Task<ActionResult<PartnerRedemptionCreatedDto>> Redeem(
        long partnerId,
        PartnerRedemptionRequest request) =>
        Ok(await sender.Send(new RedeemPartnerRewardCommand(partnerId,
            Parse<PartnerRewardMode>(request.Mode, "mode"), request.Amount,
            request.BranchId, request.WarehouseId, request.ProductVariantId,
            request.ProductQuantity, request.BusinessDate, request.Note, request.IdempotencyKey)));

    [HttpPost("{partnerId:long}/adjustments")]
    [HasPermission(AppPermissions.PartnerRewards.Adjust)]
    public async Task<ActionResult<PartnerRewardAdjustmentResult>> Adjust(
        long partnerId,
        PartnerRewardAdjustmentRequest request) =>
        Ok(await sender.Send(new AdjustPartnerRewardCommand(partnerId, request.ProgramId,
            request.Amount, request.BranchId, request.Note, request.EventId)));

    private static T Parse<T>(string value, string field) where T : struct, Enum =>
        Enum.TryParse<T>(value, true, out var result)
            ? result
            : throw new BusinessRuleException($"{field} qiymati noto'g'ri: {value}", "invalid_enum_value");
}
