namespace Cartex.Application.Common.Interfaces;

public interface ICartNotifier
{
    Task CartsChangedAsync(string kind, CancellationToken cancellationToken = default);
}
