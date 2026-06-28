using Cartex.Application.Common.Interfaces;
using Cartex.Infrastructure.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace Cartex.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<INotificationService, NullNotificationService>();
        services.AddHostedService<OutboxProcessor>();

        return services;
    }
}
