using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Transactions.Queries;

public record GetTransactionsQuery : FilteringRequest, IRequest<IReadOnlyCollection<TransactionDto>>;

public record TransactionDto(
    long Id,
    decimal Amount,
    string OperationType,
    string? FromAccountName,
    string? ToAccountName,
    DateTime CreatedAt,
    string UserName);

public sealed class GetTransactionsQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetTransactionsQuery, IReadOnlyCollection<TransactionDto>>
{
    public async Task<IReadOnlyCollection<TransactionDto>> Handle(GetTransactionsQuery request, CancellationToken cancellationToken)
    {
        return await db.Transactions
            .Include(t => t.FromAccount)
            .Include(t => t.ToAccount)
            .Include(t => t.User)
            .ToPagedListAsync(request,
                t => new TransactionDto(
                    t.Id,
                    t.Amount,
                    t.OperationType.ToString(),
                    t.FromAccount != null ? t.FromAccount.Name : null,
                    t.ToAccount != null ? t.ToAccount.Name : null,
                    t.CreatedAt,
                    t.User.FullName),
                writer, cancellationToken);
    }
}
