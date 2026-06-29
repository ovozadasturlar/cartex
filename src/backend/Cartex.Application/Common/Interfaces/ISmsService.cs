namespace Cartex.Application.Common.Interfaces;

public interface ISmsService
{
    Task SendAsync(string phone, string text, CancellationToken cancellationToken = default);
}
