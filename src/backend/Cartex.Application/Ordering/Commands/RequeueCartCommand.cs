using Cartex.Application.Common.Interfaces;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.Ordering;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Ordering.Commands;

public sealed record RequeueCartCommand(
    string Code,
    string? Note = null,
    string? IdempotencyKey = null) : ICommand<RequeueCartResult>;

public sealed class RequeueCartCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ICartNotifier notifier,
    IAuditService audit) : IRequestHandler<RequeueCartCommand, RequeueCartResult>
{
    public async Task<RequeueCartResult> Handle(RequeueCartCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.Sales.Create)
            && !currentUser.HasPermission(AppPermissions.Sales.Pick))
            throw new ForbiddenException("Savatni navbatga qaytarishga ruxsat yo'q.");
        var key = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim();
        if (key is not null)
        {
            var existing = await db.Carts.Where(x => x.IdempotencyKey == key)
                .Select(x => new RequeueCartResult(x.AggregateCode, x.Version))
                .FirstOrDefaultAsync(cancellationToken);
            if (existing is not null) return existing;
        }

        var source = await db.Carts
            .FromSqlInterpolated($"SELECT * FROM carts WHERE aggregate_code = {request.Code} FOR UPDATE")
            .Include(x => x.Items)
            .Include(x => x.Participants)
            .Include(x => x.Payments)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Cart not found.", "cart_not_found");
        if (source.Status != CartStatus.Cancelled)
            throw new ConflictException("Faqat bekor qilingan savat navbatga qaytariladi.", "cart_not_cancelled");
        var prior = await db.Carts.Where(x => x.RequeuedFromCartId == source.Id && x.Status != CartStatus.Cancelled)
            .Select(x => new RequeueCartResult(x.AggregateCode, x.Version))
            .FirstOrDefaultAsync(cancellationToken);
        if (prior is not null)
            throw new ConflictException($"Bu savat allaqachon {prior.AggregateCode} kodi bilan qaytarilgan.",
                "cart_already_requeued");

        var cart = new Cart
        {
            BranchId = source.BranchId,
            WarehouseId = source.WarehouseId,
            CustomerId = source.CustomerId,
            AggregateCode = Guid.NewGuid().ToString("N"),
            IdempotencyKey = key,
            Note = string.IsNullOrWhiteSpace(request.Note) ? source.Note : request.Note.Trim(),
            Kind = source.Kind,
            DiscountAmount = source.DiscountAmount,
            PaidCash = source.PaidCash,
            PaidCard = source.PaidCard,
            PaidBonus = source.PaidBonus,
            DebtCurrency = source.DebtCurrency,
            DebtDueDate = source.DebtDueDate,
            CreditAmount = source.CreditAmount,
            UseCustomerAdvance = source.UseCustomerAdvance,
            RequeuedFromCartId = source.Id
        };
        foreach (var row in source.Items)
            cart.Items.Add(new CartItem
            {
                VariantId = row.VariantId,
                Quantity = row.Quantity,
                UnitPriceOverride = row.UnitPriceOverride
            });
        foreach (var row in source.Participants)
            cart.Participants.Add(new CartParticipant
            {
                RoleDefinitionId = row.RoleDefinitionId,
                PartyId = row.PartyId,
                PartyNameSnapshot = row.PartyNameSnapshot,
                PartyPhoneSnapshot = row.PartyPhoneSnapshot,
                RoleLabelSnapshot = row.RoleLabelSnapshot
            });
        foreach (var row in source.Payments)
            cart.Payments.Add(new CartPayment
                { Method = row.Method, Currency = row.Currency, Amount = row.Amount });
        db.Carts.Add(cart);
        await db.SaveChangesAsync(cancellationToken);
        audit.SetOutcome("cart.requeued", "carts", cart.Id, new
        {
            sourceCartId = source.Id,
            sourceCode = source.AggregateCode,
            cart.AggregateCode,
            cart.CustomerId,
            items = cart.Items.Select(x => new { x.VariantId, x.Quantity })
        }, "Bekor qilingan savat yangi navbatga qaytarildi", cart.BranchId);
        await notifier.CartsChangedAsync(cart.Kind.ToString(), cancellationToken);
        return new RequeueCartResult(cart.AggregateCode, cart.Version);
    }
}

public sealed class RequeueCartCommandValidator : AbstractValidator<RequeueCartCommand>
{
    public RequeueCartCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.IdempotencyKey).MaximumLength(64);
    }
}
