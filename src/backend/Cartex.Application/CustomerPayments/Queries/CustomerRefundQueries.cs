using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Cartex.Shared.Models.Customers;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.CustomerPayments.Queries;

public sealed record GetCustomerRefundsQuery : FilteringRequest, IRequest<IReadOnlyCollection<CustomerRefundListDto>>
{
    public long? CustomerId { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
}

public sealed class GetCustomerRefundsQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IPagingMetadataWriter writer) : IRequestHandler<GetCustomerRefundsQuery, IReadOnlyCollection<CustomerRefundListDto>>
{
    public async Task<IReadOnlyCollection<CustomerRefundListDto>> Handle(
        GetCustomerRefundsQuery request,
        CancellationToken cancellationToken)
    {
        var query = db.CustomerRefundDocuments.AsNoTracking();
        if (!currentUser.CanAccessAllBranches)
            query = query.Where(x => currentUser.BranchIds.Contains(x.BranchId));
        if (!currentUser.HasPermission(AppPermissions.Customers.ViewAll))
            query = query.Where(x => x.Customer.AssignedUserId == currentUser.UserId);
        if (request.CustomerId is { } customerId) query = query.Where(x => x.CustomerId == customerId);
        if (request.FromDate is { } from) query = query.Where(x => x.BusinessDate >= from);
        if (request.ToDate is { } to) query = query.Where(x => x.BusinessDate <= to);

        return await query.ToPagedListAsync(request, x => new CustomerRefundListDto(
            x.Id, x.DocumentNumber, x.CustomerId, x.Customer.FullName,
            x.BusinessDate, x.CreatedAt, x.Status.ToString(), x.TotalBaseAmount,
            x.Note), writer, cancellationToken);
    }
}

public sealed record GetCustomerRefundByIdQuery(long Id) : IRequest<CustomerRefundDocumentDto>;

public sealed class GetCustomerRefundByIdQueryHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetCustomerRefundByIdQuery, CustomerRefundDocumentDto>
{
    public async Task<CustomerRefundDocumentDto> Handle(
        GetCustomerRefundByIdQuery request,
        CancellationToken cancellationToken)
    {
        var query = db.CustomerRefundDocuments.AsNoTracking().Where(x => x.Id == request.Id);
        if (!currentUser.CanAccessAllBranches)
            query = query.Where(x => currentUser.BranchIds.Contains(x.BranchId));
        if (!currentUser.HasPermission(AppPermissions.Customers.ViewAll))
            query = query.Where(x => x.Customer.AssignedUserId == currentUser.UserId);

        return await query.Select(x => new CustomerRefundDocumentDto(
                x.Id, x.DocumentNumber, x.BranchId, x.CustomerId, x.Customer.FullName,
                x.UserId, x.User.FullName, x.BusinessDate, x.CreatedAt, x.Status.ToString(),
                x.TotalBaseAmount, x.AdvanceBaseAmount, x.LoanBaseAmount, x.Note,
                x.Tenders.OrderBy(t => t.Id).Select(t => new CustomerRefundTenderDto(
                    t.Method.ToString(), t.Currency, t.Amount, t.Rate, t.AmountBase)).ToList()))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Refund document not found.", "refund_document_not_found");
    }
}
