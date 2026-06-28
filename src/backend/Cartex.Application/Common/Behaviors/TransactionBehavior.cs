using Cartex.Persistence;
using MediatR;

namespace Cartex.Application.Common.Behaviors;

public sealed class TransactionBehavior<TRequest, TResponse>(IApplicationDbContext db)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : ICommand<TResponse>
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken) =>
        db.ExecuteInTransactionAsync(() => next(), cancellationToken);
}
