using Xunit;

namespace Cartex.Application.Tests.Common;

public abstract class DatabaseTest(DatabaseFixture fixture) : IAsyncLifetime
{
    protected DatabaseFixture Fixture { get; } = fixture;

    public ValueTask InitializeAsync() => Fixture.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
