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
    public DbSet<UserBranch> UserBranches => Set<UserBranch>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<ProductType> ProductTypes => Set<ProductType>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Barcode> Barcodes => Set<Barcode>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<ProductPrice> ProductPrices => Set<ProductPrice>();
    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<LoyaltyProgram> LoyaltyPrograms => Set<LoyaltyProgram>();
    public DbSet<CashbackRule> CashbackRules => Set<CashbackRule>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<Supply> Supplies => Set<Supply>();
    public DbSet<SupplyItem> SupplyItems => Set<SupplyItem>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        if (Database.CurrentTransaction is not null)
            return await action();

        var strategy = Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
            var result = await action();
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clr = entityType.ClrType;
            if (typeof(ISoftDeletable).IsAssignableFrom(clr) || typeof(IBranchScoped).IsAssignableFrom(clr))
                ConfigureFilterMethod.MakeGenericMethod(clr).Invoke(this, [modelBuilder]);
        }

        base.OnModelCreating(modelBuilder);
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
