using Cartex.ApiClient;
using Cartex.UI.Services;
using Cartex.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Cartex.UI;

public static class DependencyInjection
{
    public static void RegisterServices(IServiceCollection services, SettingsService settings)
    {
        services.AddSingleton<ITokenStore, TokenStore>();
        services.AddSingleton<AuthService>();
        services.AddApiClients(
            () => SettingsService.Instance.ApiBaseUrl,
            () => ServiceLocator.Resolve<AuthService>().Token,
            ct => ServiceLocator.Resolve<AuthService>().EnsureFreshTokenAsync(ct),
            OnUnauthorized);

        services.AddSingleton<NavigationService>();
        services.AddSingleton<BranchContextService>();
        services.AddSingleton<ConnectivityService>();
        services.AddSingleton<OfflineStore>();
        services.AddSingleton<OfflineSyncService>();
        services.AddSingleton(LocalizationManager.Instance);

        services.AddSingleton<ToastService>();
        services.AddSingleton<IToastService>(sp => sp.GetRequiredService<ToastService>());
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<FilePickerService>();
        services.AddSingleton<IFilePickerService>(sp => sp.GetRequiredService<FilePickerService>());
        services.AddSingleton<BusyService>();
        services.AddSingleton<IBusyService>(sp => sp.GetRequiredService<BusyService>());
        services.AddSingleton<IExportService, ExportService>();
        services.AddSingleton<IHeldSaleStore, HeldSaleStore>();
        services.AddSingleton<IPrinterService, PrinterService>();
        services.AddSingleton<IBarcodeLabelService, BarcodeLabelService>();
        services.AddSingleton<IScannedCodeParser, ScannedCodeParser>();
        services.AddSingleton<ShortcutService>();
        services.AddSingleton<ReferenceCache>(sp =>
        {
            var cache = new ReferenceCache();
            var auth = sp.GetRequiredService<AuthService>();
            auth.LoggedOut += cache.Clear;
            auth.SessionInvalidated += OnUnauthorized;
            return cache;
        });

        services.AddTransient<LoginViewModel>();
        services.AddTransient<MainViewModel>();
        services.AddSingleton<SettingsHubViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddSingleton<ProductsViewModel>();
        services.AddTransient<ProductImportViewModel>();
        services.AddTransient<QuickProductViewModel>();
        services.AddTransient<PrepackViewModel>();
        services.AddSingleton<SalesViewModel>();
        services.AddSingleton<ShiftViewModel>();
        services.AddSingleton<CustomersViewModel>();
        services.AddTransient<WarehouseViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<UsersViewModel>();
        services.AddTransient<RolesViewModel>();
        services.AddSingleton<SalesHistoryViewModel>();
        services.AddSingleton<ReportsViewModel>();
        services.AddTransient<CategoriesViewModel>();
        services.AddTransient<ExpenseCategoriesViewModel>();
        services.AddTransient<UnitsViewModel>();
        services.AddTransient<ProductTypesViewModel>();
        services.AddTransient<ManufacturersViewModel>();
        services.AddTransient<BranchesViewModel>();
        services.AddTransient<WarehousesViewModel>();
        services.AddTransient<SuppliersViewModel>();
        services.AddTransient<RatesViewModel>();
        services.AddTransient<RemindersViewModel>();
        services.AddSingleton<OrdersViewModel>();
        services.AddSingleton<PosHandoffService>();
        services.AddTransient<AccountsViewModel>();
        services.AddTransient<TransactionsViewModel>();
        services.AddTransient<LoyaltyViewModel>();
        services.AddSingleton<SuppliesViewModel>();
        services.AddSingleton<TransfersViewModel>();
        services.AddTransient<PermissionsMatrixViewModel>();
        services.AddTransient<AuditViewModel>();
        services.AddTransient<TariffFeaturesViewModel>();
        services.AddTransient<IntegrationsViewModel>();
        services.AddTransient<PrintingViewModel>();
        services.AddTransient<BusinessSettingsViewModel>();
        services.AddTransient<BarcodePrintViewModel>();
        services.AddTransient<HardwareKeysViewModel>();
        services.AddTransient<DevicesViewModel>();
        services.AddTransient<OnboardingViewModel>();
    }

    private static void OnUnauthorized() =>
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var nav = ServiceLocator.Resolve<NavigationService>();
            if (nav.CurrentView is LoginViewModel) return;
            ServiceLocator.Resolve<AuthService>().Logout();
            ServiceLocator.Resolve<ToastService>().Warning(LocalizationManager.Instance["session_expired"]);
            nav.NavigateTo(ServiceLocator.Resolve<LoginViewModel>());
        });
}
