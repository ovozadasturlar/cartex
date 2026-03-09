using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Persistence.Seed;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context, Func<string, string> hashPassword)
    {
        if (await context.Roles.AnyAsync())
            return;

        var permissions = new List<Permission>
        {
            new() { Name = "shops.view", Description = "View shops" },
            new() { Name = "shops.manage", Description = "Create/edit shops" },
            new() { Name = "users.view", Description = "View users" },
            new() { Name = "users.manage", Description = "Create/edit users" },
            new() { Name = "roles.view", Description = "View roles" },
            new() { Name = "roles.manage", Description = "Create/edit roles and assign permissions" },
            new() { Name = "products.view", Description = "View products" },
            new() { Name = "products.manage", Description = "Create/edit products" },
            new() { Name = "categories.view", Description = "View categories" },
            new() { Name = "categories.manage", Description = "Create/edit categories" },
            new() { Name = "warehouses.view", Description = "View warehouses" },
            new() { Name = "warehouses.manage", Description = "Create/edit warehouses" },
            new() { Name = "stocks.view", Description = "View stock" },
            new() { Name = "stocks.manage", Description = "Manage stock" },
            new() { Name = "stock_transfers.view", Description = "View transfers" },
            new() { Name = "stock_transfers.manage", Description = "Create/manage transfers" },
            new() { Name = "sales.view", Description = "View sales" },
            new() { Name = "sales.create", Description = "Create sales (POS)" },
            new() { Name = "sales.return", Description = "Return sales" },
            new() { Name = "supplies.view", Description = "View supplies" },
            new() { Name = "supplies.manage", Description = "Create/edit supplies" },
            new() { Name = "customers.view", Description = "View customers" },
            new() { Name = "customers.manage", Description = "Create/edit customers" },
            new() { Name = "suppliers.view", Description = "View suppliers" },
            new() { Name = "suppliers.manage", Description = "Create/edit suppliers" },
            new() { Name = "accounts.view", Description = "View accounts" },
            new() { Name = "accounts.manage", Description = "Manage accounts" },
            new() { Name = "transactions.view", Description = "View transactions" },
            new() { Name = "reports.view", Description = "View reports" },
            new() { Name = "audit.view", Description = "View audit logs" },
        };

        await context.Permissions.AddRangeAsync(permissions);
        await context.SaveChangesAsync();

        var adminRole = new Role { Name = "Admin", Description = "Full access administrator" };
        var cashierRole = new Role { Name = "Cashier", Description = "POS cashier" };
        var managerRole = new Role { Name = "Manager", Description = "Shop manager" };

        await context.Roles.AddRangeAsync(adminRole, cashierRole, managerRole);
        await context.SaveChangesAsync();

        foreach (var permission in permissions)
        {
            context.RolePermissions.Add(new RolePermission { RoleId = adminRole.Id, PermissionId = permission.Id });
        }

        var cashierPermissions = new[] { "products.view", "sales.view", "sales.create", "customers.view", "stocks.view" };
        foreach (var perm in permissions.Where(p => cashierPermissions.Contains(p.Name)))
        {
            context.RolePermissions.Add(new RolePermission { RoleId = cashierRole.Id, PermissionId = perm.Id });
        }

        var managerPermissions = permissions.Where(p => !p.Name.Contains("roles.manage") && !p.Name.Contains("shops.manage")).ToList();
        foreach (var perm in managerPermissions)
        {
            context.RolePermissions.Add(new RolePermission { RoleId = managerRole.Id, PermissionId = perm.Id });
        }

        await context.SaveChangesAsync();

        var shop = new Shop { Name = "Main Shop", Address = "Tashkent", CashbackRate = 1 };
        await context.Shops.AddAsync(shop);
        await context.SaveChangesAsync();

        var warehouse = new Warehouse { ShopId = shop.Id, Name = "Main Warehouse" };
        await context.Warehouses.AddAsync(warehouse);

        var admin = new User
        {
            ShopId = shop.Id,
            FullName = "Administrator",
            Username = "admin",
            PasswordHash = hashPassword("admin123"),
            RoleId = adminRole.Id,
            IsActive = true
        };
        await context.Users.AddAsync(admin);

        var shopCashAccount = new Account
        {
            OwnerType = AccountOwnerType.Shop,
            OwnerId = shop.Id,
            Name = "Naqd Kassa",
            Type = AccountType.Cash,
            Balance = 0
        };
        var shopCardAccount = new Account
        {
            OwnerType = AccountOwnerType.Shop,
            OwnerId = shop.Id,
            Name = "Bank Karta",
            Type = AccountType.Card,
            Balance = 0
        };
        await context.Accounts.AddRangeAsync(shopCashAccount, shopCardAccount);

        var defaultUnits = new List<Unit>
        {
            new() { Name = "Dona", ShortName = "dona" },
            new() { Name = "Kilogram", ShortName = "kg" },
            new() { Name = "Litr", ShortName = "l" },
            new() { Name = "Metr", ShortName = "m" },
            new() { Name = "Qadoq", ShortName = "pak" },
        };
        await context.Units.AddRangeAsync(defaultUnits);

        await context.SaveChangesAsync();
    }
}
