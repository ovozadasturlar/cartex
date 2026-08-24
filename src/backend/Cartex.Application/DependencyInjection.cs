using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Cartex.Application.Common.Behaviors;
using Cartex.Application.Common.Finance;
using Cartex.Application.Common.Inventory;
using Cartex.Application.Common.Loyalty;
using Cartex.Application.Common.Measurement;
using Cartex.Application.Common.Participants;
using Cartex.Application.Common.Partners;
using Cartex.Application.Common.Security;
using Cartex.Application.Auth;
using Cartex.Auth.Services;
using Cartex.Application.Printing;
using Cartex.Application.OfflineCache;
using Cartex.Application.Sms;
using Cartex.Application.Notifications;

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
        services.AddScoped<IQuantityPolicyService, QuantityPolicyService>();
        services.AddScoped<IParticipantService, ParticipantService>();
        services.AddScoped<IPartnerRewardService, PartnerRewardService>();

        services.AddScoped<ICashbackCalculator, CashbackCalculator>();
        services.AddScoped<ICashbackStrategy, PercentCashbackStrategy>();
        services.AddScoped<ICashbackStrategy, FixedPerUnitCashbackStrategy>();
        services.AddScoped<IDiscountCalculator, DiscountCalculator>();

        services.AddScoped<IStockAllocator, StockAllocator>();
        services.AddScoped<Common.Sales.ISaleCorrectionPolicy, Common.Sales.SaleCorrectionPolicy>();
        services.AddScoped<IBranchCatalogService, BranchCatalogService>();
        services.AddScoped<PrintRoutingService>();
        services.AddScoped<ReceiptPrintPolicyService>();
        services.AddScoped<SmsGatewayRoutingService>();
        services.AddScoped<SmsGatewayService>();
        services.AddScoped<SmsQuotaWarningService>();
        services.AddScoped<ReceiptSmsService>();
        services.AddTransient<ReceiptSmsSaleCompletedHandler>();
        services.AddScoped<IOfflineAuthorityGuard, OfflineAuthorityGuard>();

        return services;
    }
}
