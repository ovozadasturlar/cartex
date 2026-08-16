using Cartex.Application.Common.Interfaces;
using Cartex.Application.Sales.Commands;
using Cartex.Domain.Common;
using Cartex.Domain.Authorization;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

using Cartex.Shared.Models.Ordering;
using Cartex.Application.Common.Participants;

namespace Cartex.Application.Ordering.Commands;

public record CheckoutCartCommand(
    string Code,
    decimal PaidCash,
    decimal PaidCard,
    decimal PaidBonus,
    string? IdempotencyKey = null,
    List<CheckoutCartItemDto>? Items = null,
    List<SalePaymentDto>? Payments = null,
    string? DebtCurrency = null,
    DateOnly? DebtDueDate = null,
    decimal? CreditAmount = null,
    bool? UseCustomerAdvance = null,
    long? CustomerId = null,
    decimal? DiscountAmount = null,
    string? Note = null,
    decimal? RoundingAmount = null) : ICommand<long>;

public sealed class CheckoutCartCommandHandler(
    IApplicationDbContext db,
    ISender sender,
    ICurrentUser currentUser,
    ICartNotifier notifier,
    IAuditService audit) : IRequestHandler<CheckoutCartCommand, long>
{
    public async Task<long> Handle(CheckoutCartCommand request, CancellationToken cancellationToken)
    {
        var completed = await db.ExecuteInTransactionAsync<(long SaleId, long? CartId, long? BranchId, string? CartKind)>(async () =>
        {
            var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");
            var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim();
            if (idempotencyKey is not null)
            {
                var existingSaleId = await db.Sales
                    .Where(s => s.UserId == userId && s.IdempotencyKey == idempotencyKey)
                    .Select(s => (long?)s.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (existingSaleId is not null)
                    return (existingSaleId.Value, CartId: null, BranchId: null, CartKind: null);
            }

            var cart = await db.Carts
                .FromSqlInterpolated($"SELECT * FROM carts WHERE aggregate_code = {request.Code} FOR UPDATE")
                .Include(c => c.Items)
                .Include(c => c.Participants)
                .Include(c => c.Payments)
                .FirstOrDefaultAsync(c => c.AggregateCode == request.Code, cancellationToken)
                ?? throw new NotFoundException("Cart not found.");

            if (cart.Status is CartStatus.CheckedOut or CartStatus.Cancelled)
                throw new BusinessRuleException("Savatcha allaqachon yakunlangan yoki bekor qilingan.");
            if (cart.Status is CartStatus.Confirmed or CartStatus.Ready
                && cart.ClaimedByUserId.HasValue
                && cart.ClaimedByUserId != userId
                && !currentUser.HasPermission(AppPermissions.Sales.OverrideClaim))
                throw new ConflictException("Savat boshqa kassir tomonidan olingan.", "cart_claimed_by_another_user");
            if (cart.Status == CartStatus.Open || !cart.ClaimedByUserId.HasValue)
            {
                cart.Status = CartStatus.Confirmed;
                cart.ClaimedByUserId = userId;
                cart.ClaimedAt = DateTime.UtcNow;
                cart.Version++;
            }

            cart.CustomerId = request.CustomerId ?? cart.CustomerId;
            var saleItems = request.Items is { Count: > 0 }
                ? request.Items.Select(i => new CreateSaleItemDto(i.VariantId, i.Quantity, i.UnitPrice)).ToList()
                : cart.Items.Select(i => new CreateSaleItemDto(i.VariantId, i.Quantity, i.UnitPriceOverride)).ToList();
            var preauthorizedPrices = cart.Items
                .Where(i => i.UnitPriceOverride != null)
                .ToDictionary(i => i.VariantId, i => i.UnitPriceOverride!.Value);
            var payments = request.Payments is { Count: > 0 }
                ? request.Payments
                : cart.Payments.Select(x => new SalePaymentDto(x.Method, x.Currency, x.Amount)).ToList();

            var result = await sender.Send(new CreateSaleCommand(
                cart.WarehouseId,
                cart.CustomerId,
                request.PaidCash,
                request.PaidCard,
                request.PaidBonus,
                saleItems,
                DiscountAmount: request.DiscountAmount ?? cart.DiscountAmount,
                Payments: payments,
                DebtCurrency: request.DebtCurrency ?? cart.DebtCurrency,
                DebtDueDate: request.DebtDueDate ?? cart.DebtDueDate,
                IdempotencyKey: idempotencyKey,
                CreditAmount: request.CreditAmount ?? cart.CreditAmount,
                FromQueuedCart: true,
                UseCustomerAdvance: request.UseCustomerAdvance ?? cart.UseCustomerAdvance,
                Participants: cart.Participants.Select(x =>
                    new ParticipantInput(x.RoleDefinitionId, x.PartyId)).ToList(),
                Note: request.Note ?? cart.Note,
                PreauthorizedPrices: preauthorizedPrices.Count > 0 ? preauthorizedPrices : null,
                RoundingAmount: request.RoundingAmount ?? cart.RoundingAmount,
                PreauthorizedDiscountAmount: cart.DiscountAmount,
                PreauthorizedRoundingAmount: cart.RoundingAmount), cancellationToken);

            cart.Status = CartStatus.CheckedOut;
            cart.SaleId = result.SaleId;
            cart.Version++;
            await db.SaveChangesAsync(cancellationToken);

            return (result.SaleId, CartId: cart.Id, cart.BranchId, CartKind: cart.Kind.ToString());
        }, cancellationToken);

        if (completed.CartId is not null)
            audit.SetOutcome("cart.checked_out", "carts", completed.CartId, new
            {
                request.Code,
                completed.SaleId,
                request.PaidCash,
                request.PaidCard,
                request.PaidBonus,
                request.DebtCurrency,
                request.DebtDueDate,
                request.CreditAmount,
                request.CustomerId,
                request.DiscountAmount,
                payments = request.Payments,
                items = request.Items
            }, "Savat savdo sifatida yakunlandi", completed.BranchId);
        if (completed.CartKind is not null)
            await notifier.CartsChangedAsync(completed.CartKind, cancellationToken);
        return completed.SaleId;
    }
}
