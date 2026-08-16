using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.Customers;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Customers.Queries;

/// DAL-04: the act reads and reports. It posts nothing, creates no document and changes no
/// balance — a customer asking "what did I actually use and what do I still owe" must never
/// have the answer alter the books.
public sealed record GetConsolidatedActQuery(long CustomerId, List<ConsolidatedActSelection> Documents)
    : IRequest<ConsolidatedActDto>;

public sealed class GetConsolidatedActQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ISettingsService settings) : IRequestHandler<GetConsolidatedActQuery, ConsolidatedActDto>
{
    private const string Sale = "sale";
    private const string Return = "return";
    private const string Payment = "payment";
    private const string Refund = "refund";

    public async Task<ConsolidatedActDto> Handle(GetConsolidatedActQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.Customers.Act))
            throw new ForbiddenException("Yig'ma dalolatnoma tuzishga ruxsat yo'q.");

        var policy = await settings.GetAsync<SalesPolicySettings>(SettingKeys.SalesPolicy, cancellationToken)
                     ?? new SalesPolicySettings();
        if (!policy.AllowConsolidatedAct)
            throw new BusinessRuleException("Yig'ma dalolatnoma o'chirilgan.", "consolidated_act_disabled");

        var customer = await db.Customers
            .Where(x => x.Id == request.CustomerId)
            .Select(x => new { x.Id, x.FullName, x.AssignedUserId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Customer not found.", "customer_not_found");
        if (!currentUser.HasPermission(AppPermissions.Customers.ViewAll)
            && customer.AssignedUserId != currentUser.UserId)
            throw new NotFoundException("Customer not found.", "customer_not_found");

        var ids = Selected(request.Documents);
        var rows = new List<ConsolidatedActDocumentDto>();
        var lines = new Dictionary<long, ActLine>();
        var settled = 0m;

        foreach (var sale in await LoadSalesAsync(ids[Sale], cancellationToken))
        {
            EnsureOwner(sale.CustomerId, request.CustomerId);
            rows.Add(new ConsolidatedActDocumentDto(Sale, sale.Id, sale.DocumentNumber, sale.BusinessDate, sale.TotalAmount));

            // DAL-13: however the till settled it — cash, card, bonus, advance or credit — what is
            // left unsettled is exactly the debt, so the complement is what the act calls "paid".
            settled += sale.TotalAmount - sale.DebtAmount;
            foreach (var item in sale.Items)
                Line(lines, item.VariantId, item.ProductName, item.UnitName)
                    .Add(item.Quantity, item.NetAmount);
        }

        foreach (var document in await LoadReturnsAsync(ids[Return], cancellationToken))
        {
            EnsureOwner(document.CustomerId, request.CustomerId);
            rows.Add(new ConsolidatedActDocumentDto(Return, document.Id, document.DocumentNumber,
                document.BusinessDate, -document.RefundAmount));

            // Only money that actually left the drawer comes off; a return taken against the debt
            // moved no money, and its effect is already in the goods column.
            settled -= document.CashSettled;
            foreach (var line in document.Lines)
                Line(lines, line.VariantId, line.ProductName, line.UnitName)
                    .Subtract(line.Quantity, line.NetAmount);
        }

        foreach (var document in await LoadPaymentsAsync(ids[Payment], cancellationToken))
        {
            EnsureOwner(document.CustomerId, request.CustomerId);
            rows.Add(new ConsolidatedActDocumentDto(Payment, document.Id, document.DocumentNumber,
                document.BusinessDate, document.TotalBaseAmount));
            // What went to the customer's advance settles no goods, so it stays out.
            settled += document.AllocatedBaseAmount + document.WriteOffBaseAmount;
        }

        foreach (var document in await LoadRefundsAsync(ids[Refund], cancellationToken))
        {
            EnsureOwner(document.CustomerId, request.CustomerId);
            rows.Add(new ConsolidatedActDocumentDto(Refund, document.Id, document.DocumentNumber,
                document.BusinessDate, -document.TotalBaseAmount));
            // Handing back the customer's own advance changes no goods debt; lending does.
            settled -= document.LoanBaseAmount;
        }

        if (rows.Count != request.Documents.Count)
            throw new NotFoundException("Tanlangan hujjatlardan biri topilmadi.", "act_document_not_found");

        var kept = lines.Values
            .Where(x => x.NetQuantity > 0)
            .OrderBy(x => x.ProductName)
            .Select(x => new ConsolidatedActLineDto(
                x.VariantId, x.ProductName, x.UnitName,
                x.SoldQuantity, x.ReturnedQuantity, x.NetQuantity, Math.Round(x.NetAmount, 2)))
            .ToList();

        var consumed = kept.Sum(x => x.NetAmount);
        return new ConsolidatedActDto(
            customer.Id, customer.FullName,
            rows.Min(x => x.BusinessDate), rows.Max(x => x.BusinessDate),
            rows.OrderBy(x => x.BusinessDate).ThenBy(x => x.Id).ToList(),
            kept, consumed, settled, consumed - settled);
    }

    /// DAL-03: one act, one customer. Mixing two customers produces a number that means nothing.
    private static void EnsureOwner(long? documentCustomerId, long customerId)
    {
        if (documentCustomerId != customerId)
            throw new BusinessRuleException(
                "Dalolatnomaga faqat bitta mijozning hujjatlari kiradi.", "act_customer_mismatch");
    }

    private static Dictionary<string, List<long>> Selected(List<ConsolidatedActSelection> documents)
    {
        var map = new Dictionary<string, List<long>>(StringComparer.OrdinalIgnoreCase)
        {
            [Sale] = [], [Return] = [], [Payment] = [], [Refund] = []
        };
        foreach (var selection in documents)
        {
            if (!map.TryGetValue(selection.Kind ?? "", out var bucket))
                throw new BusinessRuleException($"Hujjat turi noma'lum: {selection.Kind}", "act_unknown_kind");
            bucket.Add(selection.Id);
        }
        return map;
    }

    private static ActLine Line(Dictionary<long, ActLine> lines, long variantId, string name, string unit)
    {
        if (!lines.TryGetValue(variantId, out var line))
            lines[variantId] = line = new ActLine(variantId, name, unit);
        return line;
    }

    private async Task<List<ActSale>> LoadSalesAsync(List<long> ids, CancellationToken cancellationToken) =>
        ids.Count == 0 ? [] : await db.Sales
            .Where(x => ids.Contains(x.Id))
            .Select(x => new ActSale(
                x.Id, x.CustomerId, x.DocumentNumber, DateOnly.FromDateTime(x.CreatedAt),
                x.TotalAmount, x.DebtAmount,
                x.Items.Select(i => new ActSaleItem(
                    i.VariantId,
                    i.Variant.Product.Name,
                    i.Variant.Product.Unit.ShortName,
                    i.Quantity,
                    i.Quantity * i.UnitPrice - i.DiscountAmount)).ToList()))
            .ToListAsync(cancellationToken);

    private async Task<List<ActReturn>> LoadReturnsAsync(List<long> ids, CancellationToken cancellationToken) =>
        ids.Count == 0 ? [] : await db.CustomerReturnDocuments
            .Where(x => ids.Contains(x.Id))
            .Select(x => new ActReturn(
                x.Id, x.CustomerId, x.DocumentNumber, x.BusinessDate, x.RefundAmount,
                x.Settlements.Where(s => s.Method != ReturnSettlementMethod.ReduceDebt).Sum(s => s.AmountBase),
                x.Lines.Select(l => new ActSaleItem(
                    l.VariantId,
                    l.Variant.Product.Name,
                    l.Variant.Product.Unit.ShortName,
                    l.Quantity,
                    l.LineAmount)).ToList()))
            .ToListAsync(cancellationToken);

    private async Task<List<ActPayment>> LoadPaymentsAsync(List<long> ids, CancellationToken cancellationToken) =>
        ids.Count == 0 ? [] : await db.CustomerPaymentDocuments
            .Where(x => ids.Contains(x.Id))
            .Select(x => new ActPayment(x.Id, x.CustomerId, x.DocumentNumber, x.BusinessDate,
                x.TotalBaseAmount, x.AllocatedBaseAmount, x.WriteOffBaseAmount))
            .ToListAsync(cancellationToken);

    private async Task<List<ActRefund>> LoadRefundsAsync(List<long> ids, CancellationToken cancellationToken) =>
        ids.Count == 0 ? [] : await db.CustomerRefundDocuments
            .Where(x => ids.Contains(x.Id))
            .Select(x => new ActRefund(x.Id, x.CustomerId, x.DocumentNumber, x.BusinessDate,
                x.TotalBaseAmount, x.LoanBaseAmount))
            .ToListAsync(cancellationToken);

    private sealed record ActSaleItem(long VariantId, string ProductName, string UnitName, decimal Quantity, decimal NetAmount);
    private sealed record ActSale(long Id, long? CustomerId, string DocumentNumber, DateOnly BusinessDate,
        decimal TotalAmount, decimal DebtAmount, List<ActSaleItem> Items);
    private sealed record ActReturn(long Id, long? CustomerId, string DocumentNumber, DateOnly BusinessDate,
        decimal RefundAmount, decimal CashSettled, List<ActSaleItem> Lines);
    private sealed record ActPayment(long Id, long? CustomerId, string DocumentNumber, DateOnly BusinessDate,
        decimal TotalBaseAmount, decimal AllocatedBaseAmount, decimal WriteOffBaseAmount);
    private sealed record ActRefund(long Id, long? CustomerId, string DocumentNumber, DateOnly BusinessDate,
        decimal TotalBaseAmount, decimal LoanBaseAmount);

    private sealed class ActLine(long variantId, string productName, string unitName)
    {
        public long VariantId { get; } = variantId;
        public string ProductName { get; } = productName;
        public string UnitName { get; } = unitName;
        public decimal SoldQuantity { get; private set; }
        public decimal ReturnedQuantity { get; private set; }
        public decimal NetQuantity => SoldQuantity - ReturnedQuantity;
        public decimal NetAmount { get; private set; }

        public void Add(decimal quantity, decimal amount)
        {
            SoldQuantity += quantity;
            NetAmount += amount;
        }

        public void Subtract(decimal quantity, decimal amount)
        {
            ReturnedQuantity += quantity;
            NetAmount -= amount;
        }
    }
}

public sealed class GetConsolidatedActQueryValidator : AbstractValidator<GetConsolidatedActQuery>
{
    public GetConsolidatedActQueryValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.Documents).NotEmpty();
    }
}
