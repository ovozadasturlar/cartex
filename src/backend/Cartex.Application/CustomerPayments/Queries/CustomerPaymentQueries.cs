using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Cartex.Shared.Models.Customers;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.CustomerPayments.Queries;

public record GetCustomerPaymentsQuery : FilteringRequest, IRequest<IReadOnlyCollection<CustomerPaymentListDto>>
{
    public long? CustomerId { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
}

public sealed class GetCustomerPaymentsQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IPagingMetadataWriter writer) : IRequestHandler<GetCustomerPaymentsQuery, IReadOnlyCollection<CustomerPaymentListDto>>
{
    public async Task<IReadOnlyCollection<CustomerPaymentListDto>> Handle(
        GetCustomerPaymentsQuery request,
        CancellationToken cancellationToken)
    {
        var query = db.CustomerPaymentDocuments.AsNoTracking();
        if (!currentUser.CanAccessAllBranches)
            query = query.Where(x => currentUser.BranchIds.Contains(x.BranchId));
        if (!currentUser.HasPermission(AppPermissions.Customers.ViewAll))
            query = query.Where(x => x.Customer.AssignedUserId == currentUser.UserId);
        if (request.CustomerId is { } customerId)
            query = query.Where(x => x.CustomerId == customerId);
        if (request.FromDate is { } from)
            query = query.Where(x => x.BusinessDate >= from);
        if (request.ToDate is { } to)
            query = query.Where(x => x.BusinessDate <= to);

        return await query.ToPagedListAsync(request, x => new CustomerPaymentListDto(
            x.Id,
            x.DocumentNumber,
            x.CustomerId,
            x.Customer.FullName,
            x.BusinessDate,
            x.CreatedAt,
            x.Status.ToString(),
            x.TotalBaseAmount,
            x.AllocatedBaseAmount,
            x.AdvanceBaseAmount,
            x.Note), writer, cancellationToken);
    }
}

public record GetCustomerPaymentByIdQuery(long Id) : IRequest<CustomerPaymentDocumentDto>;

public sealed class GetCustomerPaymentByIdQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser) : IRequestHandler<GetCustomerPaymentByIdQuery, CustomerPaymentDocumentDto>
{
    public async Task<CustomerPaymentDocumentDto> Handle(GetCustomerPaymentByIdQuery request, CancellationToken cancellationToken)
    {
        var query = db.CustomerPaymentDocuments.AsNoTracking().Where(x => x.Id == request.Id);
        if (!currentUser.CanAccessAllBranches)
            query = query.Where(x => currentUser.BranchIds.Contains(x.BranchId));
        if (!currentUser.HasPermission(AppPermissions.Customers.ViewAll))
            query = query.Where(x => x.Customer.AssignedUserId == currentUser.UserId);

        var document = await query
            .Include(x => x.Tenders)
            .Include(x => x.Allocations)
            .Include(x => x.Customer)
            .Include(x => x.User)
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Payment document not found.", "payment_document_not_found");

        return new CustomerPaymentDocumentDto(
            document.Id,
            document.DocumentNumber,
            document.BranchId,
            document.CustomerId,
            document.Customer.FullName,
            document.UserId,
            document.User.FullName,
            document.BusinessDate,
            document.CreatedAt,
            document.Status.ToString(),
            document.TotalBaseAmount,
            document.AllocatedBaseAmount,
            document.AdvanceBaseAmount,
            document.Note,
            document.Tenders.Select(x => new CustomerPaymentTenderDto(
                x.Method.ToString(), x.Currency, x.Amount, x.Rate, x.AmountBase)).ToList(),
            document.Allocations.Select(x => new CustomerPaymentAllocationDto(
                x.Id, x.SaleId, x.Currency, x.Amount, x.Rate, x.AmountBase)).ToList());
    }
}
