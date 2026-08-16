using System.Text.Json;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Common.Partners;

public interface IPartnerRewardService
{
    Task AccrueSaleAsync(Sale sale, CancellationToken cancellationToken);
    Task AccruePaymentAsync(CustomerPaymentDocument document, CancellationToken cancellationToken);
    Task ReverseReturnAsync(CustomerReturnDocument document, CancellationToken cancellationToken);
    Task ReverseSaleAsync(Sale sale, CancellationToken cancellationToken);
}

internal sealed record RewardSource(
    Sale Sale,
    IReadOnlySet<PartnerRewardTrigger> Triggers,
    decimal PaymentFactor,
    CustomerPaymentDocument? PaymentDocument = null);

internal sealed record RewardDraft(
    PartnerProfile Partner,
    PartnerProgram Program,
    SaleItem Item,
    decimal Amount,
    decimal FinancialBasis,
    PartnerRewardTrigger Trigger);

internal sealed record RewardProduct(long VariantId, long ProductId, long? CategoryId);

public sealed class PartnerRewardService(IApplicationDbContext db) : IPartnerRewardService
{
    public Task AccrueSaleAsync(Sale sale, CancellationToken cancellationToken)
    {
        if (sale.Participants.Count == 0 || sale.Items.Count == 0)
            return Task.CompletedTask;

        var triggers = new HashSet<PartnerRewardTrigger> { PartnerRewardTrigger.Sale };
        var collectedFactor = sale.TotalAmount <= 0
            ? 0
            : Math.Clamp((sale.TotalAmount - sale.DebtAmount) / sale.TotalAmount, 0, 1);
        if (collectedFactor > 0)
            triggers.Add(PartnerRewardTrigger.Payment);
        return AccrueAsync([new RewardSource(sale, triggers, collectedFactor)], cancellationToken);
    }

    public async Task AccruePaymentAsync(
        CustomerPaymentDocument document,
        CancellationToken cancellationToken)
    {
        var paidBySale = document.Allocations
            .Where(x => x.SaleId.HasValue && x.AmountBase > 0)
            .GroupBy(x => x.SaleId!.Value)
            .ToDictionary(x => x.Key, x => x.Sum(row => row.AmountBase));
        if (paidBySale.Count == 0) return;

        var saleIds = paidBySale.Keys.ToList();
        var sales = await db.Sales
            .Where(x => saleIds.Contains(x.Id))
            .Include(x => x.Items)
            .Include(x => x.Participants)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        var sources = sales.Where(x => x.Participants.Count > 0 && x.Items.Count > 0)
            .Select(x => new RewardSource(
                x,
                new HashSet<PartnerRewardTrigger> { PartnerRewardTrigger.Payment },
                x.TotalAmount <= 0 ? 0 : Math.Clamp(paidBySale[x.Id] / x.TotalAmount, 0, 1),
                document))
            .Where(x => x.PaymentFactor > 0)
            .ToList();
        if (sources.Count > 0)
            await AccrueAsync(sources, cancellationToken);
    }

    private async Task AccrueAsync(
        IReadOnlyCollection<RewardSource> sources,
        CancellationToken cancellationToken)
    {
        var roleIds = sources.SelectMany(x => x.Sale.Participants)
            .Select(x => x.RoleDefinitionId).Distinct().ToList();
        var branchIds = sources.Select(x => x.Sale.BranchId).Distinct().ToList();
        var triggers = sources.SelectMany(x => x.Triggers).Distinct().ToList();
        if (roleIds.Count == 0 || triggers.Count == 0) return;

        var allPrograms = await db.PartnerPrograms
            .Where(x => x.IsEnabled && roleIds.Contains(x.RoleDefinitionId)
                        && triggers.Contains(x.Trigger)
                        && (x.BranchId == null || branchIds.Contains(x.BranchId.Value)))
            .Include(x => x.Rules)
            .ToListAsync(cancellationToken);
        if (allPrograms.Count == 0) return;

        var partyIds = sources.SelectMany(x => x.Sale.Participants)
            .Select(x => x.PartyId).Distinct().ToList();
        var partners = await db.PartnerProfiles
            .Where(x => partyIds.Contains(x.PartyId) && x.IsEnabled)
            .ToDictionaryAsync(x => x.PartyId, cancellationToken);
        if (partners.Count == 0) return;

        var variantIds = sources.SelectMany(x => x.Sale.Items)
            .Select(x => x.VariantId).Distinct().ToList();
        var products = await db.ProductVariants.Where(x => variantIds.Contains(x.Id))
            .Select(x => new RewardProduct(x.Id, x.ProductId, x.Product.CategoryId))
            .ToDictionaryAsync(x => x.VariantId, cancellationToken);

        var persistedSaleIds = sources.Select(x => x.Sale.Id).Where(x => x > 0).Distinct().ToList();
        var programIds = allPrograms.Select(x => x.Id).Distinct().ToList();
        var partnerIds = partners.Values.Select(x => x.Id).Distinct().ToList();
        var accruedByKey = persistedSaleIds.Count == 0
            ? new Dictionary<(long SaleId, long ProgramId, long PartnerId), decimal>()
            : await db.PartnerRewardEntries
                .Where(x => x.SaleId != null && persistedSaleIds.Contains(x.SaleId.Value)
                            && programIds.Contains(x.PartnerProgramId)
                            && partnerIds.Contains(x.PartnerProfileId))
                .GroupBy(x => new { SaleId = x.SaleId!.Value, x.PartnerProgramId, x.PartnerProfileId })
                .Select(x => new
                {
                    x.Key.SaleId,
                    ProgramId = x.Key.PartnerProgramId,
                    PartnerId = x.Key.PartnerProfileId,
                    Amount = x.Sum(e => e.Amount)
                })
                .ToDictionaryAsync(x => (x.SaleId, x.ProgramId, x.PartnerId), x => x.Amount,
                    cancellationToken);

        foreach (var source in sources)
        {
            var sale = source.Sale;
            foreach (var participant in sale.Participants)
            {
                if (!partners.TryGetValue(participant.PartyId, out var partner)) continue;
                var programs = SelectEffectivePrograms(allPrograms, participant.RoleDefinitionId,
                    sale.BranchId, source.Triggers);
                foreach (var program in programs)
                {
                    var eventFactor = program.Trigger == PartnerRewardTrigger.Payment
                        ? source.PaymentFactor
                        : 1m;
                    if (eventFactor <= 0) continue;
                    var drafts = BuildDrafts(sale, partner, program, products, eventFactor);
                    if (drafts.Count == 0) continue;

                    if (program.CapPerSale is { } cap)
                    {
                        var key = (sale.Id, program.Id, partner.Id);
                        var already = sale.Id > 0 ? accruedByKey.GetValueOrDefault(key) : 0;
                        var remainingCap = Math.Max(0, cap - Math.Max(0, already));
                        drafts = ApplyCap(drafts, remainingCap);
                        accruedByKey[key] = already + drafts.Sum(x => x.Amount);
                    }

                    foreach (var draft in drafts.Where(x => x.Amount > 0))
                        db.PartnerRewardEntries.Add(new PartnerRewardEntry
                        {
                            BranchId = sale.BranchId,
                            PartnerProfile = draft.Partner,
                            PartnerProgram = draft.Program,
                            Sale = sale,
                            SaleItem = draft.Item,
                            CustomerPaymentDocument = source.PaymentDocument,
                            Mode = draft.Program.Mode,
                            State = draft.Program.HoldDays > 0
                                ? PartnerRewardState.Pending
                                : PartnerRewardState.Earned,
                            Amount = draft.Amount,
                            QuantityBasis = Math.Max(0, draft.Item.Quantity - draft.Item.ReturnedQuantity),
                            FinancialBasis = draft.FinancialBasis,
                            AvailableAt = DateTime.UtcNow.AddDays(draft.Program.HoldDays),
                            DetailsJson = JsonSerializer.Serialize(new
                            {
                                draft.Program.Basis,
                                draft.Program.Value,
                                draft.Trigger,
                                participant.RoleDefinitionId,
                                participant.RoleLabelSnapshot,
                                PaymentFactor = eventFactor
                            })
                        });
                }
            }
        }
    }

    public async Task ReverseSaleAsync(Sale sale, CancellationToken cancellationToken)
    {
        var originals = await db.PartnerRewardEntries
            .Where(x => x.SaleId == sale.Id && x.Amount > 0
                        && (x.State == PartnerRewardState.Earned || x.State == PartnerRewardState.Pending))
            .ToListAsync(cancellationToken);
        if (originals.Count == 0) return;

        var originalIds = originals.Select(x => x.Id).ToList();
        var prior = await db.PartnerRewardEntries
            .Where(x => x.OriginalEntryId != null && originalIds.Contains(x.OriginalEntryId.Value))
            .GroupBy(x => x.OriginalEntryId!.Value)
            .Select(x => new { OriginalId = x.Key, Amount = x.Sum(e => e.Amount) })
            .ToDictionaryAsync(x => x.OriginalId, x => x.Amount, cancellationToken);

        foreach (var original in originals)
        {
            var remaining = Math.Max(0, original.Amount + prior.GetValueOrDefault(original.Id));
            if (remaining <= 0) continue;
            db.PartnerRewardEntries.Add(new PartnerRewardEntry
            {
                BranchId = original.BranchId,
                PartnerProfileId = original.PartnerProfileId,
                PartnerProgramId = original.PartnerProgramId,
                SaleId = original.SaleId,
                SaleItemId = original.SaleItemId,
                OriginalEntry = original,
                Mode = original.Mode,
                State = PartnerRewardState.Reversed,
                Amount = -remaining,
                QuantityBasis = original.QuantityBasis,
                FinancialBasis = -original.FinancialBasis,
                AvailableAt = DateTime.UtcNow,
                DetailsJson = JsonSerializer.Serialize(new { reason = "sale_voided", sale.ReceiptToken })
            });
        }
    }

    public async Task ReverseReturnAsync(CustomerReturnDocument document, CancellationToken cancellationToken)
    {
        var itemIds = document.Lines.Where(x => x.SaleItemId is not null)
            .Select(x => x.SaleItemId!.Value).Distinct().ToList();
        if (itemIds.Count == 0) return;
        var originals = await db.PartnerRewardEntries
            .Where(x => x.SaleItemId != null && itemIds.Contains(x.SaleItemId.Value)
                        && x.Amount > 0
                        && (x.State == PartnerRewardState.Earned || x.State == PartnerRewardState.Pending))
            .ToListAsync(cancellationToken);
        if (originals.Count == 0) return;
        var originalIds = originals.Select(x => x.Id).ToList();
        var prior = await db.PartnerRewardEntries
            .Where(x => x.OriginalEntryId != null && originalIds.Contains(x.OriginalEntryId.Value))
            .GroupBy(x => x.OriginalEntryId!.Value)
            .Select(x => new { OriginalId = x.Key, Amount = x.Sum(e => e.Amount) })
            .ToDictionaryAsync(x => x.OriginalId, x => x.Amount, cancellationToken);
        var lineByItem = document.Lines.Where(x => x.SaleItemId is not null)
            .ToDictionary(x => x.SaleItemId!.Value);
        var saleItems = await db.SaleItems.Where(x => itemIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        foreach (var original in originals)
        {
            var line = lineByItem[original.SaleItemId!.Value];
            var item = saleItems[line.SaleItemId!.Value];
            var remaining = Math.Max(0, original.Amount + prior.GetValueOrDefault(original.Id));
            if (remaining <= 0) continue;
            var isLastReturn = item.ReturnedQuantity >= item.Quantity;
            var proportional = original.QuantityBasis <= 0
                ? 0
                : Math.Round(original.Amount * line.Quantity / original.QuantityBasis, 4);
            var amount = isLastReturn ? remaining : Math.Min(remaining, proportional);
            if (amount <= 0) continue;
            db.PartnerRewardEntries.Add(new PartnerRewardEntry
            {
                BranchId = original.BranchId,
                PartnerProfileId = original.PartnerProfileId,
                PartnerProgramId = original.PartnerProgramId,
                SaleId = original.SaleId,
                SaleItemId = original.SaleItemId,
                CustomerReturnDocument = document,
                OriginalEntry = original,
                Mode = original.Mode,
                State = PartnerRewardState.Reversed,
                Amount = -amount,
                QuantityBasis = line.Quantity,
                FinancialBasis = original.QuantityBasis <= 0
                    ? 0
                    : -Math.Round(original.FinancialBasis * line.Quantity / original.QuantityBasis, 2),
                AvailableAt = DateTime.UtcNow,
                DetailsJson = JsonSerializer.Serialize(new { reason = "return", document.DocumentNumber })
            });
        }
    }

    private static List<PartnerProgram> SelectEffectivePrograms(
        IReadOnlyCollection<PartnerProgram> programs,
        long roleId,
        long branchId,
        IReadOnlySet<PartnerRewardTrigger> triggers) =>
        programs.Where(x => x.RoleDefinitionId == roleId && triggers.Contains(x.Trigger))
            .GroupBy(x => x.Trigger)
            .SelectMany(group => group.Any(x => x.BranchId == branchId)
                ? group.Where(x => x.BranchId == branchId)
                : group.Where(x => x.BranchId == null))
            .ToList();

    private static List<RewardDraft> BuildDrafts(
        Sale sale,
        PartnerProfile partner,
        PartnerProgram program,
        IReadOnlyDictionary<long, RewardProduct> products,
        decimal eventFactor)
    {
        var eligible = new List<(SaleItem Item, decimal Financial, decimal Value)>();
        foreach (var item in sale.Items)
        {
            var activeQuantity = Math.Max(0, item.Quantity - item.ReturnedQuantity);
            if (activeQuantity <= 0 || !products.TryGetValue(item.VariantId, out var product)) continue;
            var rule = ResolveRule(program, product.ProductId, product.CategoryId);
            if (rule?.IsExcluded == true) continue;
            var lineNet = item.Quantity * item.UnitPrice - item.DiscountAmount;
            var netRevenue = Math.Round(lineNet * activeQuantity / item.Quantity, 2);
            var financial = program.Basis == PartnerRewardBasis.NetMargin
                ? Math.Max(0, netRevenue - activeQuantity * item.PurchasePrice)
                : netRevenue;
            eligible.Add((item, financial, rule?.ValueOverride ?? program.Value));
        }
        if (eligible.Count == 0) return [];

        var fixedWeight = eligible.Sum(x => x.Financial > 0
            ? x.Financial
            : Math.Max(0, x.Item.Quantity - x.Item.ReturnedQuantity));
        return eligible.Select(row =>
        {
            var activeQuantity = Math.Max(0, row.Item.Quantity - row.Item.ReturnedQuantity);
            var amount = program.Basis switch
            {
                PartnerRewardBasis.NetRevenue or PartnerRewardBasis.NetMargin =>
                    row.Financial * row.Value / 100m,
                PartnerRewardBasis.FixedPerUnit => activeQuantity * row.Value,
                PartnerRewardBasis.FixedPerSale => fixedWeight <= 0
                    ? program.Value / eligible.Count
                    : program.Value * (row.Financial > 0 ? row.Financial : activeQuantity) / fixedWeight,
                _ => 0
            };
            return new RewardDraft(partner, program, row.Item,
                Math.Round(amount * eventFactor, 4),
                Math.Round(row.Financial * eventFactor, 2), program.Trigger);
        }).ToList();
    }

    private static List<RewardDraft> ApplyCap(List<RewardDraft> drafts, decimal cap)
    {
        var total = drafts.Sum(x => x.Amount);
        if (total <= cap) return drafts;
        if (cap <= 0 || total <= 0) return [];
        var capped = drafts.Select(x => x with { Amount = Math.Round(x.Amount * cap / total, 4) }).ToList();
        var excess = capped.Sum(x => x.Amount) - cap;
        if (excess > 0 && capped.Count > 0)
            capped[^1] = capped[^1] with { Amount = Math.Max(0, capped[^1].Amount - excess) };
        return capped;
    }

    private static PartnerRewardRule? ResolveRule(PartnerProgram program, long productId, long? categoryId) =>
        program.Rules.Where(x => x.Scope == CashbackScope.Product && x.TargetId == productId)
            .OrderByDescending(x => x.Priority).FirstOrDefault()
        ?? (categoryId.HasValue
            ? program.Rules.Where(x => x.Scope == CashbackScope.Category && x.TargetId == categoryId.Value)
                .OrderByDescending(x => x.Priority).FirstOrDefault()
            : null);
}
