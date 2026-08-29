using Cartex.Hub;

namespace Cartex.UnitTests;

/// Test uchun qurilma kaliti: saqlash xotirada, shuning uchun har qurilma o'z juftligini oladi.
public sealed class MemoryIdentityStore : IHubIdentityStore
{
    private string? _material;

    public Task<string?> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(_material);

    public Task WriteAsync(string material, CancellationToken cancellationToken)
    {
        _material = material;
        return Task.CompletedTask;
    }
}

public static class HubTestIdentity
{
    public static Task<HubIdentityKey> CreateAsync() => HubIdentityKey.LoadAsync(new MemoryIdentityStore());

    public static HttpClient Channel(HubIdentityKey identity) =>
        HubTransport.Create(identity, TimeSpan.FromSeconds(15));
}
