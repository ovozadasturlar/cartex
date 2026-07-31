using Cartex.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Persistence;

public interface IApplicationDbContext
{
    DbSet<Business> Businesses { get; }
    DbSet<Branch> Branches { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<User> Users { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<UserBranch> UserBranches { get; }
    DbSet<Category> Categories { get; }
    DbSet<Unit> Units { get; }
    DbSet<ProductType> ProductTypes { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductVariant> ProductVariants { get; }
    DbSet<Barcode> Barcodes { get; }
    DbSet<ProductPack> ProductPacks { get; }
    DbSet<BranchCatalogEntry> BranchCatalogEntries { get; }
    DbSet<Warehouse> Warehouses { get; }
    DbSet<Stock> Stocks { get; }
    DbSet<ProductPrice> ProductPrices { get; }
    DbSet<StockTransfer> StockTransfers { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<Account> Accounts { get; }
    DbSet<Transaction> Transactions { get; }
    DbSet<LoyaltyProgram> LoyaltyPrograms { get; }
    DbSet<DiscountRule> DiscountRules { get; }
    DbSet<Manufacturer> Manufacturers { get; }
    DbSet<CashbackRule> CashbackRules { get; }
    DbSet<Sale> Sales { get; }
    DbSet<SaleItem> SaleItems { get; }
    DbSet<Supply> Supplies { get; }
    DbSet<SupplyItem> SupplyItems { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<NotificationOutbox> NotificationOutbox { get; }
    DbSet<Cart> Carts { get; }
    DbSet<CartItem> CartItems { get; }
    DbSet<Feature> Features { get; }
    DbSet<LicenseState> LicenseStates { get; }
    DbSet<BusinessSetting> BusinessSettings { get; }
    DbSet<Shift> Shifts { get; }
    DbSet<StockAdjustment> StockAdjustments { get; }
    DbSet<ExpenseCategory> ExpenseCategories { get; }
    DbSet<ExchangeRate> ExchangeRates { get; }
    DbSet<Currency> Currencies { get; }
    DbSet<SalePayment> SalePayments { get; }
    DbSet<ShiftCash> ShiftCashes { get; }
    DbSet<DebtReminderLog> DebtReminderLogs { get; }
    DbSet<RefreshSession> RefreshSessions { get; }
    DbSet<OtpChallenge> OtpChallenges { get; }
    DbSet<CustomerSession> CustomerSessions { get; }
    DbSet<NotificationDelivery> NotificationDeliveries { get; }
    DbSet<NotificationDeliveryAttempt> NotificationDeliveryAttempts { get; }
    [Obsolete("Legacy SMS journal retained to preserve historical production data.")]
    DbSet<SmsMessage> SmsMessages { get; }
    DbSet<Prepack> Prepacks { get; }
    DbSet<HardwareKey> HardwareKeys { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default);

    Task ReloadAsync(object entity, CancellationToken cancellationToken = default);

    void RunAfterCommit(Action action);
}
