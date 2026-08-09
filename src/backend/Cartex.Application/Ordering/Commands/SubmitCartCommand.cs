using Cartex.Application.Common.Interfaces;
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

public record SubmitCartItemDto(long VariantId, decimal Quantity);

public record SubmitCartCommand(
    long WarehouseId,
    long? CustomerId,
    List<SubmitCartItemDto> Items,
    string? IdempotencyKey = null,
    string? Note = null,
    CartKind? Kind = null,
    decimal PaidCash = 0,
    decimal PaidCard = 0,
    decimal PaidBonus = 0,
    List<ParticipantInput>? Participants = null,
    List<SalePaymentDto>? Payments = null,
    string? DebtCurrency = null,
    DateOnly? DebtDueDate = null,
    decimal CreditAmount = 0,
    bool UseCustomerAdvance = true) : ICommand<string>;

public sealed class SubmitCartCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ICartNotifier notifier,
    IQuantityPolicyService quantityPolicy,
    IParticipantService participantService,
    ICurrencyService currency,
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

        if (request.CustomerId is { } customerId &&
            !await db.Customers.AnyAsync(c => c.Id == customerId, cancellationToken))
            throw new NotFoundException("Customer not found.", "customer_not_found");

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
            Kind = request.Kind ?? await ResolveKindAsync(cancellationToken),
            PaidCash = request.PaidCash,
            PaidCard = request.PaidCard,
            PaidBonus = request.PaidBonus,
            DebtCurrency = debtCurrency,
            DebtDueDate = request.DebtDueDate,
            CreditAmount = request.CreditAmount,
            UseCustomerAdvance = request.UseCustomerAdvance
        };

        foreach (var item in request.Items)
            cart.Items.Add(new CartItem { VariantId = item.VariantId, Quantity = item.Quantity });
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
        RuleFor(x => x.PaidCash).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PaidCard).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PaidBonus).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CreditAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DebtCurrency).MaximumLength(3);
        RuleFor(x => x.Payments).Must(x => x is null || x.Count <= 20);
        RuleForEach(x => x.Payments!).ChildRules(row =>
        {
            row.RuleFor(x => x.Amount).GreaterThan(0);
            row.RuleFor(x => x.Currency).MaximumLength(3);
        }).When(x => x.Payments is not null);
    }
}
