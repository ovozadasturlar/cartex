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
    DbSet<ProductReference> ProductReferences { get; }
    DbSet<ProductVariant> ProductVariants { get; }
    DbSet<Barcode> Barcodes { get; }
    DbSet<ProductPack> ProductPacks { get; }
    DbSet<BranchCatalogEntry> BranchCatalogEntries { get; }
    DbSet<Warehouse> Warehouses { get; }
    DbSet<Stock> Stocks { get; }
    DbSet<ProductPrice> ProductPrices { get; }
    DbSet<ProductPriceHistory> ProductPriceHistory { get; }
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
    DbSet<CartPayment> CartPayments { get; }
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
    DbSet<PrintNode> PrintNodes { get; }
    DbSet<PrinterEndpoint> PrinterEndpoints { get; }
    DbSet<PrintRoutingPolicy> PrintRoutingPolicies { get; }
    DbSet<PrintRouteTarget> PrintRouteTargets { get; }
    DbSet<PrintRequesterDevice> PrintRequesterDevices { get; }
    DbSet<PrintJob> PrintJobs { get; }
    DbSet<PrintAttempt> PrintAttempts { get; }
    DbSet<SmsGatewayDevice> SmsGatewayDevices { get; }
    DbSet<SmsGatewayJob> SmsGatewayJobs { get; }
    DbSet<CustomerSmsRoute> CustomerSmsRoutes { get; }
    DbSet<CustomerPaymentDocument> CustomerPaymentDocuments { get; }
    DbSet<CustomerPaymentTender> CustomerPaymentTenders { get; }
    DbSet<CustomerPaymentAllocation> CustomerPaymentAllocations { get; }
    DbSet<CustomerRefundDocument> CustomerRefundDocuments { get; }
    DbSet<CustomerRefundTender> CustomerRefundTenders { get; }
    DbSet<CustomerReturnDocument> CustomerReturnDocuments { get; }
    DbSet<CustomerReturnLine> CustomerReturnLines { get; }
    DbSet<CustomerReturnSettlement> CustomerReturnSettlements { get; }
    DbSet<InventoryPosition> InventoryPositions { get; }
    DbSet<InventoryMovement> InventoryMovements { get; }
    DbSet<Party> Parties { get; }
    DbSet<PartnerProfile> PartnerProfiles { get; }
    DbSet<ParticipantRoleDefinition> ParticipantRoleDefinitions { get; }
    DbSet<SaleParticipant> SaleParticipants { get; }
    DbSet<CartParticipant> CartParticipants { get; }
    DbSet<PartnerProgram> PartnerPrograms { get; }
    DbSet<PartnerRewardRule> PartnerRewardRules { get; }
    DbSet<PartnerRewardEntry> PartnerRewardEntries { get; }
    DbSet<PartnerRedemptionDocument> PartnerRedemptionDocuments { get; }
    DbSet<OfflineAuthorityLease> OfflineAuthorityLeases { get; }
    DbSet<OfflineSyncEvent> OfflineSyncEvents { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default);

    Task ReloadAsync(object entity, CancellationToken cancellationToken = default);

    Task<long> NextDocumentSequenceAsync(CancellationToken cancellationToken = default);

    Task UpsertInventoryPositionAsync(
        long branchId,
        Cartex.Domain.Enums.InventoryLocationKind locationKind,
        long locationId,
        long variantId,
        decimal quantity,
        long? userId,
        CancellationToken cancellationToken = default);
    Task<bool> AdjustInventoryPositionAsync(
        long branchId,
        Cartex.Domain.Enums.InventoryLocationKind locationKind,
        long locationId,
        long variantId,
        decimal delta,
        long? userId,
        CancellationToken cancellationToken = default);

    void RunAfterCommit(Action action);
}
