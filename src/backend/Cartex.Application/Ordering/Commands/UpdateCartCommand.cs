using Cartex.Application.Common.Finance;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Measurement;
using Cartex.Application.Common.Participants;
using Cartex.Application.Sales.Commands;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.Ordering.Commands;

public sealed record UpdateCartCommand(
    string Code,
    long? CustomerId,
    List<SubmitCartItemDto> Items,
    string? Note = null,
    List<ParticipantInput>? Participants = null,
    List<SalePaymentDto>? Payments = null,
    string? DebtCurrency = null,
    DateOnly? DebtDueDate = null,
    decimal CreditAmount = 0,
    bool UseCustomerAdvance = true,
    int? ExpectedVersion = null,
    decimal? DiscountAmount = null,
    decimal? RoundingAmount = null) : ICommand<Unit>;

public sealed class UpdateCartCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IQuantityPolicyService quantityPolicy,
    IParticipantService participantService,
    ICurrencyService currency,
    ICartNotifier notifier,
    IAuditService audit) : IRequestHandler<UpdateCartCommand, Unit>
{
    public async Task<Unit> Handle(UpdateCartCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.Sales.Create))
            throw new ForbiddenException("Savatni tahrirlashga ruxsat yo'q.");
        var cart = await db.Carts
            .FromSqlInterpolated($"SELECT * FROM carts WHERE aggregate_code = {request.Code} FOR UPDATE")
            .Include(x => x.Items)
            .Include(x => x.Participants)
            .Include(x => x.Payments)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Cart not found.", "cart_not_found");
        if (cart.Status != CartStatus.Open)
            throw new ConflictException("Faqat ochiq savat tahrirlanadi.", "cart_not_editable");
        if (cart.CreatedBy != currentUser.UserId && !currentUser.HasPermission(AppPermissions.Sales.ViewAll))
            throw new ForbiddenException("Boshqa foydalanuvchining savatini tahrirlashga ruxsat yo'q.");
        if (request.ExpectedVersion.HasValue && request.ExpectedVersion != cart.Version)
            throw new ConflictException("Savat boshqa qurilmada o'zgartirilgan. Yangilab qayta urinib ko'ring.", "cart_version_conflict");
        if (request.CustomerId is { } customerId
            && !await db.Customers.AnyAsync(x => x.Id == customerId, cancellationToken))
            throw new NotFoundException("Customer not found.", "customer_not_found");

        var storedOverrides = cart.Items
            .Where(i => i.UnitPriceOverride != null)
            .ToDictionary(i => i.VariantId, i => i.UnitPriceOverride!.Value);
        if (request.Items.Any(i => i.UnitPrice is not null
                && (!storedOverrides.TryGetValue(i.VariantId, out var stored) || stored != i.UnitPrice))
            && !currentUser.HasPermission(AppPermissions.Sales.PriceOverride))
            throw new ForbiddenException("Savdoda narxni o'zgartirishga ruxsat yo'q.");

        // Berilmagan maydon saqlangan qiymatida qoladi: aks holda chegirmani qayta yubormagan
        // klient uni jimgina nolga tushirib yuborardi (NAVBAT-01/02).
        var discountAmount = request.DiscountAmount ?? cart.DiscountAmount;
        var roundingAmount = request.RoundingAmount ?? cart.RoundingAmount;

        // Saqlangan qiymatni o'zgartirish ruxsat talab qiladi; tegilmagani esa yo'q.
        if ((discountAmount != cart.DiscountAmount || roundingAmount != cart.RoundingAmount)
            && (discountAmount > 0 || roundingAmount > 0)
            && !currentUser.HasPermission(AppPermissions.Sales.Discount))
            throw new ForbiddenException("Savdoda chegirma berishga ruxsat yo'q.");

        await quantityPolicy.ValidateAsync(request.Items.Select(x => (x.VariantId, x.Quantity)), cancellationToken);
        var participants = await participantService.ResolveAsync(
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

        cart.CustomerId = request.CustomerId;
        cart.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        cart.DebtCurrency = debtCurrency;
        cart.DebtDueDate = request.DebtDueDate;
        cart.CreditAmount = request.CreditAmount;
        cart.UseCustomerAdvance = request.UseCustomerAdvance;
        cart.DiscountAmount = discountAmount;
        cart.RoundingAmount = roundingAmount;
        cart.Items.Clear();
        foreach (var row in request.Items)
            cart.Items.Add(new CartItem
            {
                VariantId = row.VariantId,
                Quantity = row.Quantity,
                UnitPriceOverride = row.UnitPrice
            });
        cart.Participants.Clear();
        foreach (var row in participants)
            cart.Participants.Add(new CartParticipant
            {
                RoleDefinitionId = row.RoleDefinitionId,
                PartyId = row.PartyId,
                PartyNameSnapshot = row.PartyName,
                PartyPhoneSnapshot = row.PartyPhone,
                RoleLabelSnapshot = row.RoleLabel
            });
        cart.Payments.Clear();
        foreach (var row in payments)
            cart.Payments.Add(new CartPayment
                { Method = row.Method, Currency = row.Currency, Amount = row.Amount });
        cart.Version++;
        await db.SaveChangesAsync(cancellationToken);
        audit.SetOutcome("cart.updated", "carts", cart.Id, new
        {
            cart.AggregateCode,
            cart.Version,
            cart.CustomerId,
            cart.Note,
            items = request.Items,
            participants = cart.Participants.Select(x => new { x.RoleDefinitionId, x.PartyId }),
            payments = cart.Payments.Select(x => new { x.Method, x.Currency, x.Amount }),
            cart.DebtCurrency,
            cart.DebtDueDate,
            cart.CreditAmount,
            cart.UseCustomerAdvance
        }, "Ochiq savat yangilandi", cart.BranchId);
        await notifier.CartsChangedAsync(cart.Kind.ToString(), cancellationToken);
        return Unit.Value;
    }
}

public sealed class UpdateCartCommandValidator : AbstractValidator<UpdateCartCommand>
{
    public UpdateCartCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Items).NotEmpty().Must(x => x.Count <= 500)
            .Must(x => x.Select(i => i.VariantId).Distinct().Count() == x.Count)
            .WithMessage("Mahsulot qatorlari noto'g'ri.");
        RuleForEach(x => x.Items).Must(x => x.Quantity > 0);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.DebtCurrency).MaximumLength(3);
        RuleFor(x => x.CreditAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RoundingAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Payments).Must(x => x is null || x.Count <= 20);
        RuleForEach(x => x.Payments!).ChildRules(row =>
        {
            row.RuleFor(x => x.Amount).GreaterThan(0);
            row.RuleFor(x => x.Currency).MaximumLength(3);
        }).When(x => x.Payments is not null);
    }
}
