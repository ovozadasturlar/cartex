using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.Partners;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Partners.Queries;

public sealed record GetPartnersQuery : FilteringRequest, IRequest<IReadOnlyCollection<PartnerDto>>
{
    public bool? IsEnabled { get; init; }
}

public sealed class GetPartnersQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IPagingMetadataWriter writer) : IRequestHandler<GetPartnersQuery, IReadOnlyCollection<PartnerDto>>
{
    public async Task<IReadOnlyCollection<PartnerDto>> Handle(GetPartnersQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.Partners.View))
            throw new ForbiddenException("Hamkorlarni ko'rishga ruxsat yo'q.");
        var businessId = currentUser.BusinessId ?? throw new BusinessRuleException("Business not found.");
        var now = DateTime.UtcNow;
        var query = db.PartnerProfiles.AsNoTracking().Where(x => x.Party.BusinessId == businessId);
        if (request.IsEnabled.HasValue) query = query.Where(x => x.IsEnabled == request.IsEnabled);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x => EF.Functions.ILike(x.PartnerCode, $"%{search}%")
                                     || EF.Functions.ILike(x.Party.FullName, $"%{search}%")
                                     || (x.Party.Phone != null && EF.Functions.ILike(x.Party.Phone, $"%{search}%")));
        }

        return await query.ToPagedListAsync(request, x => new PartnerDto(
            x.Id,
            x.PartyId,
            x.PartnerCode,
            x.Party.FullName,
            x.Party.Phone,
            x.Party.Email,
            x.Party.Address,
            x.Party.CustomerProfile != null ? x.Party.CustomerProfile.Id : null,
            x.IsEnabled,
            x.JoinedAt,
            db.PartnerRewardEntries.Where(e => e.PartnerProfileId == x.Id
                                               && (e.State == PartnerRewardState.Earned
                                                   || (e.State == PartnerRewardState.Pending && e.AvailableAt <= now)
                                                   || e.State == PartnerRewardState.Reversed
                                                   || e.State == PartnerRewardState.Redeemed))
                .Sum(e => (decimal?)e.Amount) ?? 0,
            db.PartnerRewardEntries.Where(e => e.PartnerProfileId == x.Id
                                               && e.State == PartnerRewardState.Pending && e.AvailableAt > now)
                .Sum(e => (decimal?)e.Amount) ?? 0,
            -(db.PartnerRewardEntries.Where(e => e.PartnerProfileId == x.Id
                                                 && e.State == PartnerRewardState.Redeemed)
                .Sum(e => (decimal?)e.Amount) ?? 0),
            db.PartnerRewardEntries.Where(e => e.PartnerProfileId == x.Id
                                               && e.State != PartnerRewardState.Redeemed)
                .Sum(e => (decimal?)e.Amount) ?? 0,
            x.Note,
            x.PublicConsent.ToString(),
            x.PublicVisible,
            x.PublicPhoneVisible,
            x.PublicDisplayName,
            x.PublicAbout),
            writer, cancellationToken);
    }
}

public sealed record GetCustomerPartnerQuery(long CustomerId) : IRequest<CustomerPartnerDto?>;

public sealed class GetCustomerPartnerQueryHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetCustomerPartnerQuery, CustomerPartnerDto?>
{
    public async Task<CustomerPartnerDto?> Handle(GetCustomerPartnerQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.Partners.View))
            throw new ForbiddenException("Hamkorlarni ko'rishga ruxsat yo'q.");
        var businessId = currentUser.BusinessId ?? throw new BusinessRuleException("Business not found.");

        return await db.PartnerProfiles.AsNoTracking()
            .Where(x => x.Party.BusinessId == businessId
                        && x.Party.CustomerProfile != null
                        && x.Party.CustomerProfile.Id == request.CustomerId)
            .Select(x => new CustomerPartnerDto(x.Id, x.IsEnabled,
                x.PublicConsent.ToString(), x.PublicVisible, x.PublicPhoneVisible,
                x.PublicDisplayName, x.PublicAbout))
            .FirstOrDefaultAsync(cancellationToken);
    }
}

public sealed record GetParticipantRolesQuery(bool IncludeDisabled = false)
    : IRequest<IReadOnlyCollection<ParticipantRoleDto>>;

public sealed class GetParticipantRolesQueryHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetParticipantRolesQuery, IReadOnlyCollection<ParticipantRoleDto>>
{
    public async Task<IReadOnlyCollection<ParticipantRoleDto>> Handle(GetParticipantRolesQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.Partners.View))
            throw new ForbiddenException("Hamkor rollarini ko'rishga ruxsat yo'q.");
        var businessId = currentUser.BusinessId ?? throw new BusinessRuleException("Business not found.");
        return await db.ParticipantRoleDefinitions.AsNoTracking()
            .Where(x => x.BusinessId == businessId && (request.IncludeDisabled || x.IsEnabled))
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
            .Select(x => new ParticipantRoleDto(x.Id, x.Key, x.SingularLabel, x.PluralLabel,
                x.IsEnabled, x.IsRequired, x.CanEqualBuyer, x.MaxCount,
                x.AppliesToCart, x.AppliesToSale, x.SortOrder))
            .ToListAsync(cancellationToken);
    }
}

public sealed record GetPartnerProgramsQuery(bool IncludeDisabled = false)
    : IRequest<IReadOnlyCollection<PartnerProgramDto>>;

public sealed class GetPartnerProgramsQueryHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetPartnerProgramsQuery, IReadOnlyCollection<PartnerProgramDto>>
{
    public async Task<IReadOnlyCollection<PartnerProgramDto>> Handle(GetPartnerProgramsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.PartnerRewards.View))
            throw new ForbiddenException("Hamkor mukofotlarini ko'rishga ruxsat yo'q.");
        var businessId = currentUser.BusinessId ?? throw new BusinessRuleException("Business not found.");
        return await db.PartnerPrograms.AsNoTracking()
            .Where(x => x.BusinessId == businessId && (request.IncludeDisabled || x.IsEnabled))
            .OrderBy(x => x.Name)
            .Select(x => new PartnerProgramDto(x.Id, x.RoleDefinitionId, x.RoleDefinition.SingularLabel,
                x.Name, x.IsEnabled, x.Mode.ToString(), x.Basis.ToString(), x.Trigger.ToString(),
                x.Value, x.BranchId, x.Branch != null ? x.Branch.Name : null, x.CapPerSale, x.HoldDays,
                x.Rules.OrderByDescending(r => r.Priority).Select(r => new PartnerRewardRuleRequest(
                    r.Scope.ToString(), r.TargetId, r.IsExcluded, r.ValueOverride, r.Priority)).ToList()))
            .ToListAsync(cancellationToken);
    }
}

public sealed record GetPartnerRankingQuery(DateTime? From = null, DateTime? To = null, int Take = 100)
    : IRequest<IReadOnlyCollection<PartnerRankingDto>>;

public sealed class GetPartnerRankingQueryHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetPartnerRankingQuery, IReadOnlyCollection<PartnerRankingDto>>
{
    public async Task<IReadOnlyCollection<PartnerRankingDto>> Handle(GetPartnerRankingQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.PartnerRewards.View))
            throw new ForbiddenException("Hamkor reytingini ko'rishga ruxsat yo'q.");
        var businessId = currentUser.BusinessId ?? throw new BusinessRuleException("Business not found.");
        var now = DateTime.UtcNow;
        var entries = db.PartnerRewardEntries.AsNoTracking()
            .Where(x => x.PartnerProfile.Party.BusinessId == businessId);
        if (request.From.HasValue) entries = entries.Where(x => x.CreatedAt >= request.From.Value);
        if (request.To.HasValue) entries = entries.Where(x => x.CreatedAt < request.To.Value);

        var rows = await entries.GroupBy(x => new
            {
                x.PartnerProfileId, x.PartnerProfile.PartyId,
                x.PartnerProfile.PartnerCode, x.PartnerProfile.Party.FullName
            })
            .Select(g => new
            {
                g.Key.PartnerProfileId, g.Key.PartyId, g.Key.PartnerCode, g.Key.FullName,
                Earned = g.Where(x => x.State == PartnerRewardState.Earned
                                      || (x.State == PartnerRewardState.Pending && x.AvailableAt <= now)
                                      || x.State == PartnerRewardState.Reversed
                                      || x.State == PartnerRewardState.Redeemed).Sum(x => x.Amount),
                Pending = g.Where(x => x.State == PartnerRewardState.Pending && x.AvailableAt > now).Sum(x => x.Amount),
                Redeemed = -g.Where(x => x.State == PartnerRewardState.Redeemed).Sum(x => x.Amount),
                Score = g.Where(x => x.State != PartnerRewardState.Redeemed).Sum(x => x.Amount),
                SalesCount = g.Where(x => x.SaleId != null && x.Amount > 0).Select(x => x.SaleId).Distinct().Count()
            })
            .OrderByDescending(x => x.Score).ThenBy(x => x.FullName)
            .Take(Math.Clamp(request.Take, 1, 500))
            .ToListAsync(cancellationToken);
        return rows.Select((x, index) => new PartnerRankingDto(index + 1,
            x.PartnerProfileId, x.PartyId, x.PartnerCode, x.FullName,
            x.Earned, x.Pending, x.Redeemed, x.Score, x.SalesCount)).ToList();
    }
}

public sealed record GetPartnerRewardEntriesQuery(long PartnerId) : FilteringRequest,
    IRequest<IReadOnlyCollection<PartnerRewardEntryDto>>;

public sealed class GetPartnerRewardEntriesQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IPagingMetadataWriter writer)
    : IRequestHandler<GetPartnerRewardEntriesQuery, IReadOnlyCollection<PartnerRewardEntryDto>>
{
    public async Task<IReadOnlyCollection<PartnerRewardEntryDto>> Handle(
        GetPartnerRewardEntriesQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.PartnerRewards.View))
            throw new ForbiddenException("Hamkor mukofotlarini ko'rishga ruxsat yo'q.");
        var businessId = currentUser.BusinessId ?? throw new BusinessRuleException("Business not found.");
        if (!await db.PartnerProfiles.AnyAsync(x => x.Id == request.PartnerId
                && x.Party.BusinessId == businessId, cancellationToken))
            throw new NotFoundException("Partner not found.", "partner_not_found");

        return await db.PartnerRewardEntries.AsNoTracking()
            .Where(x => x.PartnerProfileId == request.PartnerId)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .ToPagedListAsync(request, x => new PartnerRewardEntryDto(
                x.Id,
                x.CreatedAt,
                x.Mode.ToString(),
                x.State.ToString(),
                x.Amount,
                x.AvailableAt,
                x.SaleId,
                x.SaleItem != null ? x.SaleItem.Variant.Product.Name : null,
                x.CustomerPaymentDocumentId,
                x.CustomerReturnDocumentId,
                x.PartnerRedemptionDocumentId,
                x.PartnerProgram.Name,
                x.DetailsJson), writer, cancellationToken);
    }
}
