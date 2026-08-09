using Cartex.Domain.Common;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Common.Participants;

public sealed record ParticipantInput(long RoleDefinitionId, long PartyId);

public enum ParticipantContext
{
    Cart,
    Sale,
    TradeCase
}

public sealed record ResolvedParticipant(
    long RoleDefinitionId,
    long PartyId,
    string PartyName,
    string? PartyPhone,
    string RoleLabel);

public interface IParticipantService
{
    Task<IReadOnlyList<ResolvedParticipant>> ResolveAsync(
        IReadOnlyCollection<ParticipantInput>? inputs,
        ParticipantContext context,
        long? buyerCustomerId,
        CancellationToken cancellationToken);
}

public sealed class ParticipantService(
    IApplicationDbContext db,
    ICurrentUser currentUser) : IParticipantService
{
    public async Task<IReadOnlyList<ResolvedParticipant>> ResolveAsync(
        IReadOnlyCollection<ParticipantInput>? inputs,
        ParticipantContext context,
        long? buyerCustomerId,
        CancellationToken cancellationToken)
    {
        var businessId = currentUser.BusinessId
            ?? await db.Businesses.Select(x => (long?)x.Id).FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleException("Business not found.");
        var roleQuery = db.ParticipantRoleDefinitions
            .Where(x => x.BusinessId == businessId && x.IsEnabled);
        roleQuery = context switch
        {
            ParticipantContext.Cart => roleQuery.Where(x => x.AppliesToCart),
            ParticipantContext.Sale => roleQuery.Where(x => x.AppliesToSale),
            ParticipantContext.TradeCase => roleQuery.Where(x => x.AppliesToTradeCase),
            _ => roleQuery.Where(_ => false)
        };
        var roles = await roleQuery.ToListAsync(cancellationToken);
        var rows = inputs?.ToList() ?? [];
        if (rows.Select(x => new { x.RoleDefinitionId, x.PartyId }).Distinct().Count() != rows.Count)
            throw new BusinessRuleException("Bir xil ishtirokchi takrorlangan.", "duplicate_participant");

        var roleById = roles.ToDictionary(x => x.Id);
        foreach (var group in rows.GroupBy(x => x.RoleDefinitionId))
        {
            if (!roleById.TryGetValue(group.Key, out var role))
                throw new BusinessRuleException("Ishtirokchi roli faol emas.", "participant_role_unavailable");
            if (group.Count() > role.MaxCount)
                throw new BusinessRuleException($"{role.SingularLabel} soni {role.MaxCount} tadan oshmasligi kerak.", "participant_limit_exceeded");
        }
        var missing = roles.FirstOrDefault(x => x.IsRequired && rows.All(row => row.RoleDefinitionId != x.Id));
        if (missing is not null)
            throw new BusinessRuleException($"{missing.SingularLabel} tanlanishi shart.", "required_participant_missing");
        if (rows.Count == 0) return [];

        var partyIds = rows.Select(x => x.PartyId).Distinct().ToList();
        var parties = await db.Parties
            .Where(x => partyIds.Contains(x.Id) && x.BusinessId == businessId
                        && x.PartnerProfile != null && x.PartnerProfile.IsEnabled)
            .Select(x => new { x.Id, x.FullName, x.Phone })
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        if (parties.Count != partyIds.Count)
            throw new BusinessRuleException("Hamkor topilmadi yoki faol emas.", "partner_unavailable");

        long? buyerPartyId = null;
        if (buyerCustomerId.HasValue)
            buyerPartyId = await db.Customers.Where(x => x.Id == buyerCustomerId.Value)
                .Select(x => (long?)x.PartyId).FirstOrDefaultAsync(cancellationToken);

        var result = new List<ResolvedParticipant>(rows.Count);
        foreach (var row in rows)
        {
            var role = roleById[row.RoleDefinitionId];
            if (!role.CanEqualBuyer && buyerPartyId == row.PartyId)
                throw new BusinessRuleException($"{role.SingularLabel} xaridorning o'zi bo'la olmaydi.", "participant_cannot_equal_buyer");
            var party = parties[row.PartyId];
            result.Add(new ResolvedParticipant(role.Id, party.Id, party.FullName, party.Phone, role.SingularLabel));
        }
        return result;
    }
}
