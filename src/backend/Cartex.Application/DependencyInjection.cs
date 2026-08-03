using FluentValidation;
using Cartex.Application.Common.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Cartex.Application.Common.Behaviors;
using Cartex.Application.Common.Finance;
using Cartex.Application.Common.Inventory;
using Cartex.Application.Common.Loyalty;
using Cartex.Application.Common.Security;
using Cartex.Application.Auth;
using Cartex.Auth.Services;
using Cartex.Application.Printing;

namespace Cartex.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediator(assembly);
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        services.AddScoped<IAccessControlService, AccessControlService>();
        services.AddScoped<AuthTokenBuilder>();
        services.AddScoped<IActiveRoleValidator, ActiveRoleValidator>();
        services.AddScoped<Store.StoreTokenBuilder>();

        services.AddScoped<ILedgerService, LedgerService>();
        services.AddScoped<ICurrencyService, CurrencyService>();

        services.AddScoped<ICashbackCalculator, CashbackCalculator>();
        services.AddScoped<ICashbackStrategy, PercentCashbackStrategy>();
        services.AddScoped<ICashbackStrategy, FixedPerUnitCashbackStrategy>();
        services.AddScoped<IDiscountCalculator, DiscountCalculator>();

        services.AddScoped<IStockAllocator, StockAllocator>();
        services.AddScoped<IBranchCatalogService, BranchCatalogService>();
        services.AddScoped<PrintRoutingService>();

        return services;
    }
}
