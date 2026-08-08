using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Transactions.Queries;

public record GetTransactionsQuery : FilteringRequest, IRequest<IReadOnlyCollection<TransactionDto>>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? OperationType { get; set; }
}

public record TransactionDto(
    long Id,
    decimal Amount,
    string Currency,
    string OperationType,
    string? FromAccountName,
    string? ToAccountName,
    DateTime CreatedAt,
    string UserName);

public sealed class GetTransactionsQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IPagingMetadataWriter writer) : IRequestHandler<GetTransactionsQuery, IReadOnlyCollection<TransactionDto>>
{
    public async Task<IReadOnlyCollection<TransactionDto>> Handle(GetTransactionsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Transactions.AsQueryable();
        if (!currentUser.CanAccessAllBranches)
            query = query.Where(t => t.BranchId == null || currentUser.BranchIds.Contains(t.BranchId.Value));

        if (request.FromDate is { } fromDate)
            query = query.Where(t => t.CreatedAt >= DateTime.SpecifyKind(fromDate, DateTimeKind.Utc));
        if (request.ToDate is { } toDate)
            query = query.Where(t => t.CreatedAt < DateTime.SpecifyKind(toDate, DateTimeKind.Utc));
        if (!string.IsNullOrEmpty(request.OperationType) && Enum.TryParse<Cartex.Domain.Enums.OperationType>(request.OperationType, out var ot))
            query = query.Where(t => t.OperationType == ot);

        return await query
            .Include(t => t.FromAccount)
            .Include(t => t.ToAccount)
            .Include(t => t.User)
            .ToPagedListAsync(request,
                t => new TransactionDto(
                    t.Id,
                    t.Amount,
                    t.Currency,
                    t.OperationType.ToString(),
                    t.FromAccount != null ? t.FromAccount.Name : null,
                    t.ToAccount != null ? t.ToAccount.Name : null,
                    t.CreatedAt,
                    t.User.FullName),
                writer, cancellationToken);
    }
}
