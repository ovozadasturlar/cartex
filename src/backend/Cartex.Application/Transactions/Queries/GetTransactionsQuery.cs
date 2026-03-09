using MediatR;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;

namespace Cartex.Application.Transactions.Queries;

public record GetTransactionsQuery(DateTime? FromDate, DateTime? ToDate) : IRequest<List<TransactionDto>>;

public record TransactionDto(
    long Id,
    decimal Amount,
    string OperationType,
    string? FromAccountName,
    string? ToAccountName,
    DateTime CreatedAt,
    string UserName);

public sealed class GetTransactionsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetTransactionsQuery, List<TransactionDto>>
{
    public async Task<List<TransactionDto>> Handle(GetTransactionsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Transactions
            .Include(t => t.FromAccount)
            .Include(t => t.ToAccount)
            .Include(t => t.User)
            .AsQueryable();

        if (request.FromDate is not null)
            query = query.Where(t => t.CreatedAt >= request.FromDate);

        if (request.ToDate is not null)
            query = query.Where(t => t.CreatedAt <= request.ToDate);

        return await query
            .Select(t => new TransactionDto(
                t.Id,
                t.Amount,
                t.OperationType.ToString(),
                t.FromAccount != null ? t.FromAccount.Name : null,
                t.ToAccount != null ? t.ToAccount.Name : null,
                t.CreatedAt,
                t.User.FullName))
            .ToListAsync(cancellationToken);
    }
}
