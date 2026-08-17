using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Application.Common.Measurement;
using Cartex.Application.Common.Participants;
using Cartex.Application.Common.Finance;
using Cartex.Application.Sales.Commands;

namespace Cartex.Application.Ordering.Commands;

public record SubmitCartItemDto(long VariantId, decimal Quantity, decimal? UnitPrice = null);

public sealed record SubmitCartCommand(long WarehouseId, long? CustomerId, List<SubmitCartItemDto> Items) : ICommand<string>
{
    public string? IdempotencyKey { get; init; }
    public string? Note { get; init; }
    public CartKind? Kind { get; init; }
    public decimal PaidCash { get; init; }
    public decimal PaidCard { get; init; }
    public decimal PaidBonus { get; init; }
    public List<ParticipantInput>? Participants { get; init; }
    public List<SalePaymentDto>? Payments { get; init; }
    public string? DebtCurrency { get; init; }
    public DateOnly? DebtDueDate { get; init; }
    public decimal CreditAmount { get; init; }
    public bool UseCustomerAdvance { get; init; } = true;
    public decimal DiscountAmount { get; init; }
}

public sealed class SubmitCartCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ICartNotifier notifier,
    IQuantityPolicyService quantityPolicy,
    IParticipantService participantService,
    ICurrencyService currency,
    ISettingsService settings,
    IAuditService audit) : IRequestHandler<SubmitCartCommand, string>
{
    public async Task<string> Handle(SubmitCartCommand request, CancellationToken cancellationToken)
    {
        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim();
        if (idempotencyKey is not null)
        {
            var existing = await db.Carts
                .Where(c => c.IdempotencyKey == idempotencyKey)
                .Select(c => c.AggregateCode)
                .FirstOrDefaultAsync(cancellationToken);
            if (existing is not null)
                return existing;
        }

        var warehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken)
            ?? throw new NotFoundException("Warehouse not found.", "warehouse_not_found");

        // NAVBAT-06: navbat do'konning ish uslubi. O'chirilgan bo'lsa server savatni navbatga
        // qo'ymaydi — klientda ikonani yashirish qoida emas. Proforma bundan tashqarida:
        // u qog'oz chiqarish uchun saqlanadi, navbatga tushmaydi (NAVBAT-07).
        var kind = request.Kind ?? await ResolveKindAsync(cancellationToken);
        if (kind == CartKind.Queue)
        {
            var policy = await settings.GetAsync<SalesPolicySettings>(SettingKeys.SalesPolicy, cancellationToken)
                ?? new SalesPolicySettings();
            if (!policy.AllowSaleQueue)
                throw new BusinessRuleException(
                    "Savatni navbatga qo'yish do'kon siyosatida yopilgan.", "sale_queue_disabled");
        }

        if (request.CustomerId is { } customerId &&
            !await db.Customers.AnyAsync(c => c.Id == customerId, cancellationToken))
            throw new NotFoundException("Customer not found.", "customer_not_found");

        if (request.Items.Any(x => x.UnitPrice is not null)
            && !currentUser.HasPermission(AppPermissions.Sales.PriceOverride))
            throw new ForbiddenException("Savdoda narxni o'zgartirishga ruxsat yo'q.");

        // Ruxsat kiritilayotgan joyda tekshiriladi: yakunlashda savatdagi chegirma allaqachon
        // ruxsat berilgan deb qabul qilinadi, shuning uchun bu yerda o'tkazib yuborilsa
        // ruxsatsiz sotuvchi chegirmani kassir orqali o'tkazib yuborishi mumkin bo'lardi.
        if (request.DiscountAmount > 0
            && !currentUser.HasPermission(AppPermissions.Sales.Discount))
            throw new ForbiddenException("Savdoda chegirma berishga ruxsat yo'q.");

        await quantityPolicy.ValidateAsync(
            request.Items.Select(x => (x.VariantId, x.Quantity)), cancellationToken);
        var resolvedParticipants = await participantService.ResolveAsync(
            request.Participants, ParticipantContext.Cart, request.CustomerId, cancellationToken);
        var baseCurrency = (await currency.BaseAsync(cancellationToken)).ToUpperInvariant();
        var payments = new List<SalePaymentDto>();
        foreach (var row in request.Payments ?? [])
        {
            var code = string.IsNullOrWhiteSpace(row.Currency)
                ? baseCurrency
                : row.Currency.Trim().ToUpperInvariant();
            await currency.EnsureSalesAllowedAsync(code, cancellationToken);
            if (row.Method == PaymentMethod.Bonus && code != baseCurrency)
                throw new BusinessRuleException("Bonus faqat asosiy valyutada ishlatiladi.", "bonus_currency_invalid");
            payments.Add(row with { Currency = code });
        }
        var debtCurrency = string.IsNullOrWhiteSpace(request.DebtCurrency)
            ? null
            : request.DebtCurrency.Trim().ToUpperInvariant();
        if (debtCurrency is not null)
            await currency.EnsureSalesAllowedAsync(debtCurrency, cancellationToken);

        var cart = new Cart
        {
            BranchId = warehouse.BranchId,
            WarehouseId = request.WarehouseId,
            CustomerId = request.CustomerId,
            AggregateCode = Guid.NewGuid().ToString("N"),
            IdempotencyKey = idempotencyKey,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            Kind = kind,
            DiscountAmount = request.DiscountAmount,
            PaidCash = request.PaidCash,
            PaidCard = request.PaidCard,
            PaidBonus = request.PaidBonus,
            DebtCurrency = debtCurrency,
            DebtDueDate = request.DebtDueDate,
            CreditAmount = request.CreditAmount,
            UseCustomerAdvance = request.UseCustomerAdvance
        };

        foreach (var item in request.Items)
            cart.Items.Add(new CartItem
            {
                VariantId = item.VariantId,
                Quantity = item.Quantity,
                UnitPriceOverride = item.UnitPrice
            });
        foreach (var participant in resolvedParticipants)
            cart.Participants.Add(new CartParticipant
            {
                RoleDefinitionId = participant.RoleDefinitionId,
                PartyId = participant.PartyId,
                PartyNameSnapshot = participant.PartyName,
                PartyPhoneSnapshot = participant.PartyPhone,
                RoleLabelSnapshot = participant.RoleLabel
            });
        foreach (var payment in payments)
            cart.Payments.Add(new CartPayment
            {
                Method = payment.Method,
                Currency = payment.Currency,
                Amount = payment.Amount
            });

        db.Carts.Add(cart);
        await db.SaveChangesAsync(cancellationToken);
        audit.SetOutcome("cart.submitted", "carts", cart.Id, new
        {
            cart.AggregateCode,
            cart.Kind,
            cart.WarehouseId,
            cart.CustomerId,
            cart.PaidCash,
            cart.PaidCard,
            cart.PaidBonus,
            cart.DebtCurrency,
            cart.DebtDueDate,
            cart.CreditAmount,
            cart.Note,
            items = request.Items,
            payments = cart.Payments.Select(x => new { x.Method, x.Currency, x.Amount }),
            participants = cart.Participants.Select(x => new
                { x.RoleDefinitionId, x.PartyId, x.PartyNameSnapshot, x.RoleLabelSnapshot })
        }, "Savat navbatga yuborildi", warehouse.BranchId);
        await notifier.CartsChangedAsync(cart.Kind.ToString(), cancellationToken);

        return cart.AggregateCode;
    }

    private async Task<CartKind> ResolveKindAsync(CancellationToken cancellationToken)
    {
        var destination = await db.Users
            .Where(u => u.Id == currentUser.UserId)
            .Select(u => u.CartDestination
                ?? u.UserRoles.OrderByDescending(ur => ur.Role.Priority).Select(ur => ur.Role.CartDestination).FirstOrDefault())
            .FirstOrDefaultAsync(cancellationToken);
        return string.Equals(destination, "order", StringComparison.OrdinalIgnoreCase) ? CartKind.Order : CartKind.Queue;
    }
}

public sealed class SubmitCartCommandValidator : AbstractValidator<SubmitCartCommand>
{
    public SubmitCartCommandValidator()
    {
        RuleFor(x => x.Items).NotEmpty();
        RuleFor(x => x.Items).Must(x => x.Count <= 500).WithMessage("Bitta savatda 500 tadan ortiq qator bo'lishi mumkin emas.");
        RuleFor(x => x.Items).Must(x => x.Select(i => i.VariantId).Distinct().Count() == x.Count)
            .WithMessage("Bir mahsulot varianti savatda takrorlanmasligi kerak.");
        RuleForEach(x => x.Items).Must(i => i.Quantity > 0).WithMessage("Miqdor 0 dan katta bo'lishi kerak.");
        RuleForEach(x => x.Items).Must(i => i.UnitPrice is null or >= 0).WithMessage("Narx manfiy bo'lishi mumkin emas.");
        RuleFor(x => x.PaidCash).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PaidCard).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PaidBonus).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CreditAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DebtCurrency).MaximumLength(3);
        RuleFor(x => x.Payments).Must(x => x is null || x.Count <= 20);
        RuleForEach(x => x.Payments!).ChildRules(row =>
        {
            row.RuleFor(x => x.Amount).GreaterThan(0);
            row.RuleFor(x => x.Currency).MaximumLength(3);
        }).When(x => x.Payments is not null);
    }
}
