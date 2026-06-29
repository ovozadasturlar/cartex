using Xunit;

namespace Cartex.Application.Tests.Common;

public abstract class DatabaseTest(DatabaseFixture fixture) : IAsyncLifetime
{
    public ValueTask InitializeAsync() => fixture.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
