using System.Linq.Expressions;
using System.Reflection;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly bool _branchFilterDisabled;
    private readonly long[] _accessibleBranchIds;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentUser currentUser)
        : base(options)
    {
        _branchFilterDisabled = !currentUser.IsAuthenticated || currentUser.CanAccessAllBranches;
        _accessibleBranchIds = [.. currentUser.BranchIds];
    }

    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserBranch> UserBranches => Set<UserBranch>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<ProductType> ProductTypes => Set<ProductType>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<Barcode> Barcodes => Set<Barcode>();
    public DbSet<ProductPack> ProductPacks => Set<ProductPack>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<BranchCatalogEntry> BranchCatalogEntries => Set<BranchCatalogEntry>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<ProductPrice> ProductPrices => Set<ProductPrice>();
    public DbSet<ProductPriceHistory> ProductPriceHistory => Set<ProductPriceHistory>();
    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<LoyaltyProgram> LoyaltyPrograms => Set<LoyaltyProgram>();
    public DbSet<DiscountRule> DiscountRules => Set<DiscountRule>();
    public DbSet<Manufacturer> Manufacturers => Set<Manufacturer>();
    public DbSet<CashbackRule> CashbackRules => Set<CashbackRule>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<Supply> Supplies => Set<Supply>();
    public DbSet<SupplyItem> SupplyItems => Set<SupplyItem>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<NotificationOutbox> NotificationOutbox => Set<NotificationOutbox>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<CartPayment> CartPayments => Set<CartPayment>();
    public DbSet<Feature> Features => Set<Feature>();
    public DbSet<LicenseState> LicenseStates => Set<LicenseState>();
    public DbSet<BusinessSetting> BusinessSettings => Set<BusinessSetting>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<SalePayment> SalePayments => Set<SalePayment>();
    public DbSet<ShiftCash> ShiftCashes => Set<ShiftCash>();
    public DbSet<DebtReminderLog> DebtReminderLogs => Set<DebtReminderLog>();
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();
    public DbSet<CustomerSession> CustomerSessions => Set<CustomerSession>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();
    public DbSet<NotificationDeliveryAttempt> NotificationDeliveryAttempts => Set<NotificationDeliveryAttempt>();
    [Obsolete("Legacy SMS journal retained to preserve historical production data.")]
    public DbSet<SmsMessage> SmsMessages => Set<SmsMessage>();
    public DbSet<Prepack> Prepacks => Set<Prepack>();
    public DbSet<HardwareKey> HardwareKeys => Set<HardwareKey>();
    public DbSet<PrintNode> PrintNodes => Set<PrintNode>();
    public DbSet<PrinterEndpoint> PrinterEndpoints => Set<PrinterEndpoint>();
    public DbSet<PrintRoutingPolicy> PrintRoutingPolicies => Set<PrintRoutingPolicy>();
    public DbSet<PrintRouteTarget> PrintRouteTargets => Set<PrintRouteTarget>();
    public DbSet<PrintRequesterDevice> PrintRequesterDevices => Set<PrintRequesterDevice>();
    public DbSet<PrintJob> PrintJobs => Set<PrintJob>();
    public DbSet<PrintAttempt> PrintAttempts => Set<PrintAttempt>();
    public DbSet<CustomerPaymentDocument> CustomerPaymentDocuments => Set<CustomerPaymentDocument>();
    public DbSet<CustomerPaymentTender> CustomerPaymentTenders => Set<CustomerPaymentTender>();
    public DbSet<CustomerPaymentAllocation> CustomerPaymentAllocations => Set<CustomerPaymentAllocation>();
    public DbSet<CustomerRefundDocument> CustomerRefundDocuments => Set<CustomerRefundDocument>();
    public DbSet<CustomerRefundTender> CustomerRefundTenders => Set<CustomerRefundTender>();
    public DbSet<CustomerReturnDocument> CustomerReturnDocuments => Set<CustomerReturnDocument>();
    public DbSet<CustomerReturnLine> CustomerReturnLines => Set<CustomerReturnLine>();
    public DbSet<CustomerReturnSettlement> CustomerReturnSettlements => Set<CustomerReturnSettlement>();
    public DbSet<InventoryPosition> InventoryPositions => Set<InventoryPosition>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<Party> Parties => Set<Party>();
    public DbSet<PartnerProfile> PartnerProfiles => Set<PartnerProfile>();
    public DbSet<ParticipantRoleDefinition> ParticipantRoleDefinitions => Set<ParticipantRoleDefinition>();
    public DbSet<SaleParticipant> SaleParticipants => Set<SaleParticipant>();
    public DbSet<CartParticipant> CartParticipants => Set<CartParticipant>();
    public DbSet<PartnerProgram> PartnerPrograms => Set<PartnerProgram>();
    public DbSet<PartnerRewardRule> PartnerRewardRules => Set<PartnerRewardRule>();
    public DbSet<PartnerRewardEntry> PartnerRewardEntries => Set<PartnerRewardEntry>();
    public DbSet<PartnerRedemptionDocument> PartnerRedemptionDocuments => Set<PartnerRedemptionDocument>();
    public DbSet<OfflineAuthorityLease> OfflineAuthorityLeases => Set<OfflineAuthorityLease>();
    public DbSet<OfflineSyncEvent> OfflineSyncEvents => Set<OfflineSyncEvent>();

    private readonly List<Action> _afterCommit = [];

    public void RunAfterCommit(Action action)
    {
        if (Database.CurrentTransaction is null) action();
        else _afterCommit.Add(action);
    }

    public Task ReloadAsync(object entity, CancellationToken cancellationToken = default)
    {
        var entry = Entry(entity);
        if (entry.State is EntityState.Modified or EntityState.Added)
            return Task.CompletedTask;
        return entry.ReloadAsync(cancellationToken);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        if (Database.CurrentTransaction is not null)
            return await action();

        var strategy = Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _afterCommit.Clear();
            await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
            var result = await action();
            await transaction.CommitAsync(cancellationToken);
            foreach (var deferred in _afterCommit) deferred();
            _afterCommit.Clear();
            return result;
        });
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.HasSequence<long>("document_number_seq");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clr = entityType.ClrType;
            if (typeof(ISoftDeletable).IsAssignableFrom(clr) || typeof(IBranchScoped).IsAssignableFrom(clr))
                ConfigureFilterMethod.MakeGenericMethod(clr).Invoke(this, [modelBuilder]);
        }

        base.OnModelCreating(modelBuilder);
    }

    public Task<long> NextDocumentSequenceAsync(CancellationToken cancellationToken = default) =>
        Database.SqlQueryRaw<long>("SELECT nextval('document_number_seq') AS \"Value\"")
            .SingleAsync(cancellationToken);

    public Task UpsertInventoryPositionAsync(
        long branchId,
        Cartex.Domain.Enums.InventoryLocationKind locationKind,
        long locationId,
        long variantId,
        decimal quantity,
        long? userId,
        CancellationToken cancellationToken = default) =>
        Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO inventory_positions
                (branch_id, location_kind, location_id, variant_id, quantity, created_at, created_by)
            VALUES
                ({branchId}, {locationKind.ToString()}, {locationId}, {variantId}, {quantity}, {DateTime.UtcNow}, {userId})
            ON CONFLICT (branch_id, location_kind, location_id, variant_id)
            DO UPDATE SET quantity = inventory_positions.quantity + EXCLUDED.quantity,
                          updated_at = EXCLUDED.created_at,
                          updated_by = EXCLUDED.created_by
            """, cancellationToken);

    public async Task<bool> AdjustInventoryPositionAsync(
        long branchId,
        Cartex.Domain.Enums.InventoryLocationKind locationKind,
        long locationId,
        long variantId,
        decimal delta,
        long? userId,
        CancellationToken cancellationToken = default)
    {
        if (delta >= 0)
        {
            await UpsertInventoryPositionAsync(branchId, locationKind, locationId, variantId,
                delta, userId, cancellationToken);
            return true;
        }

        var affected = await Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE inventory_positions
               SET quantity = quantity + {delta},
                   updated_at = {DateTime.UtcNow},
                   updated_by = {userId}
             WHERE branch_id = {branchId}
               AND location_kind = {locationKind.ToString()}
               AND location_id = {locationId}
               AND variant_id = {variantId}
               AND quantity + {delta} >= 0
            """, cancellationToken);
        return affected == 1;
    }

    private static readonly MethodInfo ConfigureFilterMethod =
        typeof(ApplicationDbContext).GetMethod(nameof(ConfigureGlobalFilter), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private void ConfigureGlobalFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class
    {
        Expression<Func<TEntity, bool>>? filter = null;

        if (typeof(ISoftDeletable).IsAssignableFrom(typeof(TEntity)))
            filter = e => !EF.Property<bool>(e, nameof(ISoftDeletable.IsDeleted));

        if (typeof(IBranchScoped).IsAssignableFrom(typeof(TEntity)))
        {
            Expression<Func<TEntity, bool>> branchFilter =
                e => _branchFilterDisabled || _accessibleBranchIds.Contains(EF.Property<long>(e, nameof(IBranchScoped.BranchId)));
            filter = filter is null ? branchFilter : Combine(filter, branchFilter);
        }

        if (filter is not null)
            modelBuilder.Entity<TEntity>().HasQueryFilter(filter);
    }

    private static Expression<Func<T, bool>> Combine<T>(Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
    {
        var parameter = Expression.Parameter(typeof(T), "e");
        var body = Expression.AndAlso(
            new ReplaceParameterVisitor(left.Parameters[0], parameter).Visit(left.Body),
            new ReplaceParameterVisitor(right.Parameters[0], parameter).Visit(right.Body));
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    private sealed class ReplaceParameterVisitor(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }
}
