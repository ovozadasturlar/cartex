using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Cartex.Application.Common.Messaging;

public static class MediatorServiceCollectionExtensions
{
    public static IServiceCollection AddMediator(this IServiceCollection services, Assembly assembly)
    {
        services.AddTransient<IMediator, Mediator>();
        services.AddTransient<ISender>(sp => sp.GetRequiredService<IMediator>());
        services.AddTransient<IPublisher>(sp => sp.GetRequiredService<IMediator>());

        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface) continue;

            foreach (var i in type.GetInterfaces().Where(i => i.IsGenericType))
            {
                var def = i.GetGenericTypeDefinition();
                if (def == typeof(IRequestHandler<,>) || def == typeof(INotificationHandler<>))
                    services.AddTransient(i, type);
            }
        }

        return services;
    }
}
