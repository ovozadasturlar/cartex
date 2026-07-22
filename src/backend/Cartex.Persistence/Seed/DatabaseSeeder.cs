using Cartex.Domain.Authorization;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Persistence.Seed;

public static class DatabaseSeeder
{
    public static readonly string[] AdminGrant =
        [.. AppPermissions.Catalog.Keys.Where(k => !AppPermissions.DeveloperOnly.Contains(k))];

    public static readonly string[] SellerPermissions =
    [
        AppPermissions.Products.View, AppPermissions.Categories.View, AppPermissions.Sales.View,
        AppPermissions.Sales.Create, AppPermissions.Sales.Discount, AppPermissions.Sales.Prepack,
        AppPermissions.Shifts.Manage, AppPermissions.Shifts.View, AppPermissions.Customers.View, AppPermissions.Customers.ViewAll,
        AppPermissions.Stocks.View, AppPermissions.Branches.View, AppPermissions.Warehouses.View,
        AppPermissions.Devices.Manage
    ];

    public static readonly string[] AgentPermissions =
    [
        AppPermissions.Products.View, AppPermissions.Categories.View, AppPermissions.Sales.View, AppPermissions.Sales.Create,
        AppPermissions.Shifts.Manage, AppPermissions.Shifts.View, AppPermissions.Customers.View,
        AppPermissions.Customers.Manage, AppPermissions.Stocks.View, AppPermissions.StockTransfers.View,
        AppPermissions.Branches.View, AppPermissions.Warehouses.View, AppPermissions.Devices.Manage
    ];

    public static readonly (string Name, string ShortName, UnitDimension Dimension, decimal Factor, bool IsDefault)[] SystemUnits =
    [
        ("Dona", "dona", UnitDimension.Count, 1, true),
        ("Kilogram", "kg", UnitDimension.Weight, 1000, true),
        ("Litr", "l", UnitDimension.Volume, 1000, true),
        ("Metr", "m", UnitDimension.Length, 1000, true),
        ("Gramm", "g", UnitDimension.Weight, 1, false),
        ("Tonna", "t", UnitDimension.Weight, 1000000, false),
        ("Millilitr", "ml", UnitDimension.Volume, 1, false),
        ("Santimetr", "sm", UnitDimension.Length, 10, false),
        ("Millimetr", "mm", UnitDimension.Length, 1, false),
    ];

    public static async Task SyncUnitsAsync(ApplicationDbContext context)
    {
        var existing = (await context.Units.IgnoreQueryFilters().Select(u => u.ShortName).ToListAsync()).ToHashSet();
        var missing = SystemUnits
            .Where(u => !existing.Contains(u.ShortName))
            .Select(u => new Unit { Name = u.Name, ShortName = u.ShortName, Dimension = u.Dimension, Factor = u.Factor, IsSystem = true, IsDefault = u.IsDefault })
            .ToList();

        if (missing.Count > 0)
        {
            await context.Units.AddRangeAsync(missing);
            await context.SaveChangesAsync();
        }
    }

    public static async Task SyncPermissionsAsync(ApplicationDbContext context)
    {
        if (!await context.Roles.AnyAsync())
            return;

        var permissions = await context.Permissions.ToListAsync();
        var existingNames = permissions.Select(p => p.Name).ToHashSet();
        var missing = AppPermissions.Catalog
            .Where(kv => !existingNames.Contains(kv.Key))
            .Select(kv => new Permission { Name = kv.Key, Description = kv.Value })
            .ToList();

        if (missing.Count > 0)
        {
            await context.Permissions.AddRangeAsync(missing);
            await context.SaveChangesAsync();
            permissions.AddRange(missing);
        }

        var permByName = permissions.ToDictionary(p => p.Name, p => p.Id);

        if (permByName.TryGetValue(AppPermissions.Settings.Manage, out var legacyId))
        {
            string[] replacements = [AppPermissions.Settings.Integrations, AppPermissions.Settings.Receipt, AppPermissions.Settings.Security];
            var legacyGrants = await context.RolePermissions.Where(rp => rp.PermissionId == legacyId).ToListAsync();
            var roleIds = legacyGrants.Select(rp => rp.RoleId).ToHashSet();
            var pairs = (await context.RolePermissions.Where(rp => roleIds.Contains(rp.RoleId))
                .Select(rp => new { rp.RoleId, rp.PermissionId }).ToListAsync())
                .Select(x => (x.RoleId, x.PermissionId)).ToHashSet();
            foreach (var roleId in roleIds)
                foreach (var name in replacements)
                    if (pairs.Add((roleId, permByName[name])))
                        context.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permByName[name] });
            context.RolePermissions.RemoveRange(legacyGrants);
            context.Permissions.Remove(permissions.First(p => p.Id == legacyId));
            permByName.Remove(AppPermissions.Settings.Manage);

            var allRoles = await context.Roles.ToListAsync();
            foreach (var role in allRoles.Where(r => r.GrantablePermissions.Contains(AppPermissions.Settings.Manage)))
                role.GrantablePermissions = [.. role.GrantablePermissions.Where(p => p != AppPermissions.Settings.Manage).Union(replacements)];

            await context.SaveChangesAsync();
        }

        async Task GrantAsync(string roleName, IEnumerable<string> names, string[]? grantable = null)
        {
            var role = await context.Roles.Include(r => r.RolePermissions)
                .FirstOrDefaultAsync(r => r.Name == roleName);
            if (role is null || role.AccessAll)
                return;

            var have = role.RolePermissions.Select(rp => rp.PermissionId).ToHashSet();
            foreach (var name in names)
                if (permByName.TryGetValue(name, out var pid) && have.Add(pid))
                    context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = pid });

            if (grantable is not null)
            {
                var missingGrantable = grantable.Except(role.GrantablePermissions).ToList();
                if (missingGrantable.Count > 0)
                    role.GrantablePermissions = [.. role.GrantablePermissions, .. missingGrantable];
            }
        }

        if (!await context.Roles.AnyAsync(r => r.Name == AppRoles.Agent))
        {
            context.Roles.Add(new Role { Name = AppRoles.Agent, Description = "Savdo agenti (mobil)", StartPage = "pos", CartDestination = "order", Priority = AppRoles.AgentLevel, Level = AppRoles.AgentLevel, IsSystem = true });
            await context.SaveChangesAsync();
        }

        await GrantAsync(AppRoles.Admin, AdminGrant, AdminGrant);
        await GrantAsync(AppRoles.Seller, SellerPermissions);
        await GrantAsync(AppRoles.Agent, AgentPermissions);
        await context.SaveChangesAsync();
    }

    public static async Task SyncCurrenciesAsync(ApplicationDbContext context)
    {
        var business = await context.Businesses.FirstOrDefaultAsync();
        if (business is null)
            return;

        var existing = await context.Currencies.Select(c => c.Code).ToListAsync();
        (string Code, string Name)[] system = [(business.Currency, ""), ("USD", "AQSH dollari"), ("EUR", "Yevro"), ("RUB", "Rossiya rubli")];
        foreach (var (code, name) in system)
            if (!existing.Contains(code))
                context.Currencies.Add(new Currency { Code = code, Name = name, IsSystem = true, IsDefault = code == "USD" });
        await context.SaveChangesAsync();
    }

    public static async Task SyncStorageDefaultAsync(ApplicationDbContext context)
    {
        if (await context.BusinessSettings.AnyAsync(s => s.Key == "storage"))
            return;

        context.BusinessSettings.Add(new BusinessSetting { Key = "storage", Value = """{"Enabled":true,"Provider":"local"}""" });
        await context.SaveChangesAsync();
    }

    public static async Task SyncFeaturesAsync(ApplicationDbContext context)
    {
        if (!await context.Roles.AnyAsync())
            return;

        var existing = (await context.Features.Select(f => f.Code).ToListAsync()).ToHashSet();
        var missing = FeatureCatalog.Names
            .Where(kv => !existing.Contains(kv.Key))
            .Select(kv => new Feature { Code = kv.Key, Name = kv.Value, IsEnabled = !FeatureCatalog.DefaultDisabled.Contains(kv.Key) })
            .ToList();

        var stale = await context.Features.Where(f => !FeatureCatalog.AllCodes.Contains(f.Code)).ToListAsync();

        if (missing.Count > 0 || stale.Count > 0)
        {
            await context.Features.AddRangeAsync(missing);
            context.Features.RemoveRange(stale);
            await context.SaveChangesAsync();
        }
    }

    public static async Task EnsureDeveloperPasswordAsync(ApplicationDbContext context, Func<string, string, bool> verify, Func<string, string> hashPassword, string? developerPassword)
    {
        if (string.IsNullOrWhiteSpace(developerPassword))
            return;

        var developer = await context.Users.FirstOrDefaultAsync(u => u.Username == "developer");
        if (developer is not null && verify("developer123", developer.PasswordHash))
        {
            developer.PasswordHash = hashPassword(developerPassword);
            await context.SaveChangesAsync();
        }
    }

    public static async Task EnsureAdminPasswordAsync(ApplicationDbContext context, Func<string, string, bool> verify, Func<string, string> hashPassword, string? adminPassword)
    {
        if (string.IsNullOrWhiteSpace(adminPassword))
            return;

        var admin = await context.Users.FirstOrDefaultAsync(u => u.Username == "admin");
        if (admin is not null && verify("admin123", admin.PasswordHash))
        {
            admin.PasswordHash = hashPassword(adminPassword);
            await context.SaveChangesAsync();
        }
    }

    public static async Task SeedAsync(ApplicationDbContext context, Func<string, string> hashPassword, string? developerPassword = null, string? adminPassword = null, bool seedSeller = true)
    {
        if (await context.Roles.AnyAsync())
            return;

        var permissions = AppPermissions.Catalog
            .Select(kv => new Permission { Name = kv.Key, Description = kv.Value })
            .ToList();

        await context.Permissions.AddRangeAsync(permissions);
        await context.Features.AddRangeAsync(FeatureCatalog.Names
            .Select(kv => new Feature { Code = kv.Key, Name = kv.Value, IsEnabled = !FeatureCatalog.DefaultDisabled.Contains(kv.Key) }));
        await context.LicenseStates.AddAsync(new LicenseState { Tariff = "pro", ExpiresAt = null });
        await context.SaveChangesAsync();

        var developerRole = new Role { Name = AppRoles.Developer, Description = "Vendor / tizim ishlab chiquvchi", StartPage = "dashboard", Priority = AppRoles.DeveloperLevel, Level = AppRoles.DeveloperLevel, IsSystem = true, AccessAll = true };
        var adminRole = new Role { Name = AppRoles.Admin, Description = "Biznes egasi", StartPage = "dashboard", Priority = AppRoles.AdminLevel, Level = AppRoles.AdminLevel, IsSystem = true, GrantablePermissions = [.. AdminGrant] };
        var sellerRole = new Role { Name = AppRoles.Seller, Description = "Sotuvchi (kassa)", StartPage = "pos", CartDestination = "queue", Priority = AppRoles.SellerLevel, Level = AppRoles.SellerLevel, IsSystem = true };
        var agentRole = new Role { Name = AppRoles.Agent, Description = "Savdo agenti (mobil)", StartPage = "pos", CartDestination = "order", Priority = AppRoles.AgentLevel, Level = AppRoles.AgentLevel, IsSystem = true };

        await context.Roles.AddRangeAsync(developerRole, adminRole, sellerRole, agentRole);
        await context.SaveChangesAsync();

        foreach (var perm in permissions.Where(p => AdminGrant.Contains(p.Name)))
        {
            context.RolePermissions.Add(new RolePermission { RoleId = adminRole.Id, PermissionId = perm.Id });
        }

        foreach (var perm in permissions.Where(p => SellerPermissions.Contains(p.Name)))
        {
            context.RolePermissions.Add(new RolePermission { RoleId = sellerRole.Id, PermissionId = perm.Id });
        }

        foreach (var perm in permissions.Where(p => AgentPermissions.Contains(p.Name)))
        {
            context.RolePermissions.Add(new RolePermission { RoleId = agentRole.Id, PermissionId = perm.Id });
        }

        await context.SaveChangesAsync();

        var business = new Business { Name = "Cartex Biznes", Currency = "UZS" };
        await context.Businesses.AddAsync(business);
        await context.BusinessSettings.AddAsync(new BusinessSetting { Key = "onboarded", Value = "true" });
        await context.SaveChangesAsync();

        await context.LoyaltyPrograms.AddAsync(new LoyaltyProgram { IsEnabled = true, TotalPercent = 1 });

        var branch = new Branch { BusinessId = business.Id, Name = "Asosiy filial" };
        await context.Branches.AddAsync(branch);
        await context.SaveChangesAsync();

        var warehouse = new Warehouse { BranchId = branch.Id, Name = "Asosiy ombor" };

        var developer = new User
        {
            FullName = "Developer",
            Username = AppRoles.Developer,
            PasswordHash = hashPassword(string.IsNullOrWhiteSpace(developerPassword) ? "developer123" : developerPassword),
            UserRoles = [new UserRole { RoleId = developerRole.Id }],
            DefaultBranchId = branch.Id,
            IsActive = true
        };
        var admin = new User
        {
            FullName = "Administrator",
            Username = AppRoles.Admin,
            PasswordHash = hashPassword(string.IsNullOrWhiteSpace(adminPassword) ? "admin123" : adminPassword),
            UserRoles = [new UserRole { RoleId = adminRole.Id }],
            DefaultBranchId = branch.Id,
            IsActive = true
        };
        await context.Warehouses.AddAsync(warehouse);
        await context.Users.AddRangeAsync(developer, admin);
        await context.SaveChangesAsync();

        await context.UserBranches.AddRangeAsync(
            new UserBranch { UserId = developer.Id, BranchId = branch.Id },
            new UserBranch { UserId = admin.Id, BranchId = branch.Id });

        if (seedSeller)
        {
            var seller = new User
            {
                FullName = "Sotuvchi",
                Username = AppRoles.Seller,
                PasswordHash = hashPassword("seller123"),
                UserRoles = [new UserRole { RoleId = sellerRole.Id }],
                DefaultBranchId = branch.Id,
                IsActive = true
            };
            await context.Users.AddAsync(seller);
            await context.SaveChangesAsync();
            await context.UserBranches.AddAsync(new UserBranch { UserId = seller.Id, BranchId = branch.Id });
        }

        var cashAccount = new Account { BranchId = branch.Id, Name = "Naqd kassa", Type = AccountType.Cash, Balance = 0 };
        var cardAccount = new Account { BranchId = branch.Id, Name = "Bank karta", Type = AccountType.Card, Balance = 0 };
        await context.Accounts.AddRangeAsync(cashAccount, cardAccount);

        var defaultUnits = SystemUnits
            .Select(u => new Unit { Name = u.Name, ShortName = u.ShortName, Dimension = u.Dimension, Factor = u.Factor, IsSystem = true, IsDefault = u.IsDefault })
            .ToList();
        await context.Units.AddRangeAsync(defaultUnits);

        var typeRegular = new ProductType { Name = "Oddiy mahsulot", TracksExpiry = false };
        var typeExpiring = new ProductType { Name = "Muddatli mahsulot", TracksExpiry = true };
        await context.ProductTypes.AddRangeAsync(typeRegular, typeExpiring);

        await context.SaveChangesAsync();
    }
}
