using Cartex.Application.Common.Sales;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.Sales;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Sales.Queries;

public sealed record GetSaleByIdQuery(long Id) : IRequest<SaleDetailDto>;

public sealed class GetSaleByIdQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ISaleCorrectionPolicy correctionPolicy) : IRequestHandler<GetSaleByIdQuery, SaleDetailDto>
{
    public async Task<SaleDetailDto> Handle(GetSaleByIdQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.Sales.View))
            throw new ForbiddenException("Savdoni ko'rishga ruxsat yo'q.");
        var query = db.Sales.AsNoTracking().Where(x => x.Id == request.Id);
        if (!currentUser.HasPermission(AppPermissions.Sales.ViewAll))
            query = query.Where(x => x.UserId == currentUser.UserId);

        var sale = await query.Select(x => new SaleDetailDto(
            x.Id,
            x.CreatedAt,
            x.Status.ToString(),
            x.ReceiptToken,
            x.BranchId,
            x.Warehouse.Branch.Name,
            x.WarehouseId,
            x.Warehouse.Name,
            x.UserId,
            x.User.FullName,
            x.CustomerId,
            x.Customer != null ? x.Customer.FullName : null,
            x.Customer != null ? x.Customer.Phone : null,
            x.TotalAmount,
            x.DiscountAmount,
            x.PaidCash,
            x.PaidCard,
            x.PaidBonus,
            x.PaidAdvance,
            x.DebtAmount,
            x.DebtCurrency,
            x.ChangeAmount,
            x.CreditAmount,
            x.CashbackEarned,
            x.Items.OrderBy(i => i.Id).Select(i => new SaleDetailItemDto(
                i.Id,
                i.VariantId,
                i.Variant.Product.Name,
                i.Variant.Name,
                i.Variant.Product.Unit.ShortName,
                i.Quantity,
                i.ReturnedQuantity,
                Math.Max(0, i.Quantity - i.ReturnedQuantity),
                i.UnitPrice,
                i.PriceCurrency,
                i.PriceRate,
                i.Quantity * i.UnitPrice,
                i.DiscountAmount,
                i.Quantity * i.UnitPrice - i.DiscountAmount,
                i.CashbackEarned,
                i.ReturnedCashback,
                i.Variant.Product.FractionalOverride ?? i.Variant.Product.Unit.AllowFractional)).ToList(),
            x.Payments.OrderBy(p => p.Id).Select(p => new SaleDetailPaymentDto(
                p.Method.ToString(), p.Currency, p.Amount, p.Rate, p.AmountBase)).ToList(),
            x.Participants.OrderBy(p => p.Id).Select(p => new SaleDetailParticipantDto(
                p.RoleDefinitionId, p.PartyId, p.RoleLabelSnapshot, p.PartyNameSnapshot,
                p.PartyPhoneSnapshot, p.Source.ToString())).ToList(),
            db.CustomerReturnDocuments.Where(r => r.Lines.Any(line => line.SaleId == x.Id))
                .OrderByDescending(r => r.BusinessDate).ThenByDescending(r => r.Id)
                .Select(r => new SaleReturnSummaryDto(r.Id, r.DocumentNumber, r.BusinessDate,
                    r.RefundAmount, r.Status.ToString())).ToList(),
            new List<string>(),
            x.Note,
            x.VoidReason))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sale not found.", "sale_not_found");

        var actions = new List<string>();
        if (currentUser.HasPermission(AppPermissions.Printing.ReceiptPrint)) actions.Add("printReceipt");
        if (currentUser.HasPermission(AppPermissions.Printing.ReceiptReprint)) actions.Add("reprintReceipt");
        if (sale.CustomerId.HasValue && currentUser.HasPermission(AppPermissions.Customers.Message))
            actions.Add("resendReceipt");
        if (sale.Items.Any(x => x.ReturnableQuantity > 0)
            && currentUser.HasPermission(AppPermissions.Returns.Create))
            actions.Add("createReturn");
        if (sale.CustomerId.HasValue && currentUser.HasPermission(AppPermissions.Customers.View))
            actions.Add("openCustomer");
        if (currentUser.HasPermission(AppPermissions.Sales.Void)
            && await correctionPolicy.CanCorrectAsync(sale.Id, cancellationToken))
            actions.Add("correctSale");
        return sale with { AllowedActions = actions };
    }
}
