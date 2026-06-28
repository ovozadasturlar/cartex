using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Cartex.Application.Common.Behaviors;
using Cartex.Application.Common.Finance;
using Cartex.Application.Common.Inventory;
using Cartex.Application.Common.Loyalty;

namespace Cartex.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        services.AddScoped<ILedgerService, LedgerService>();

        services.AddScoped<ICashbackCalculator, CashbackCalculator>();
        services.AddScoped<ICashbackStrategy, PercentCashbackStrategy>();
        services.AddScoped<ICashbackStrategy, FixedPerUnitCashbackStrategy>();

        services.AddScoped<IStockAllocator, StockAllocator>();

        return services;
    }
}
