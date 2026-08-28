namespace Cartex.Application.Common.Interfaces;

public interface ICartNotifier
{
    Task CartsChangedAsync(long branchId, string kind, CancellationToken cancellationToken = default);
}
