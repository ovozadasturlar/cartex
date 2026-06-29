namespace Cartex.Application.Common.Interfaces;

public interface IHardwareKeyService
{
    Task<string> IssueAsync(string username, string serial, CancellationToken cancellationToken = default);
    Task<string?> VerifyAsync(string keyContent, string serial, CancellationToken cancellationToken = default);
}
