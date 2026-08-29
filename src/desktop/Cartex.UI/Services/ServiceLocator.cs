using Microsoft.Extensions.DependencyInjection;

namespace Cartex.UI.Services;

public static class ServiceLocator
{
    private static IServiceProvider? _provider;

    public static void Initialize(IServiceProvider provider) =>
        _provider = provider;

    public static T Resolve<T>() where T : notnull =>
        (_provider ?? throw new InvalidOperationException("ServiceLocator not initialized"))
            .GetRequiredService<T>();

    public static T? TryResolve<T>() where T : class => _provider?.GetService<T>();

    public static object Resolve(Type type) =>
        (_provider ?? throw new InvalidOperationException("ServiceLocator not initialized"))
            .GetRequiredService(type);
}
