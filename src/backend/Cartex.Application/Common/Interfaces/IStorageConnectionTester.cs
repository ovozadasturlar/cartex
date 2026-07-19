using Cartex.Application.Common.Settings;

namespace Cartex.Application.Common.Interfaces;

public interface IStorageConnectionTester
{
    Task EnsureReachableAsync(StorageSettings candidate, CancellationToken cancellationToken = default);
}
