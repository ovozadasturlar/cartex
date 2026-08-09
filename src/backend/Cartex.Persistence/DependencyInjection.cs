using Cartex.Persistence.Interceptors;
using Cartex.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Cartex.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddScoped<AuditScopeState>();
        services.AddScoped<IAuditService, AuditService>();

        services.AddScoped<ISaveChangesInterceptor, SoftDeleteInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, BranchStampingInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, EntityAuditInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, DomainEventOutboxInterceptor>();

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
            options
                .UseNpgsql(connectionString, npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
                .UseSnakeCaseNamingConvention()
                .ConfigureWarnings(w => w.Ignore(
                    CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning,
                    CoreEventId.FirstWithoutOrderByAndFilterWarning,
                    RelationalEventId.PendingModelChangesWarning))
                .AddInterceptors(sp.GetServices<ISaveChangesInterceptor>()));

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());

        return services;
    }
}
