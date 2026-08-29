using Cartex.Persistence;

namespace Cartex.Application.Common.Behaviors;

public sealed class TransactionBehavior<TRequest, TResponse>(IApplicationDbContext db, IAuditService audit)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : ICommand<TResponse>
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken) =>
        db.ExecuteInTransactionAsync(async () =>
        {
            var scope = audit.BeginCommand(typeof(TRequest).Name);
            try
            {
                var response = await next(cancellationToken);
                await audit.CompleteCommandAsync(scope, cancellationToken);
                return response;
            }
            catch
            {
                audit.AbortCommand(scope);
                throw;
            }
        }, cancellationToken);
}
