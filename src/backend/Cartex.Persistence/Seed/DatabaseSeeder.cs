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
        AppPermissions.Products.View, AppPermissions.Categories.View, AppPermissions.Sales.View, AppPermissions.Sales.ViewAll,
        AppPermissions.Sales.Create, AppPermissions.Sales.Checkout, AppPermissions.Sales.Discount, AppPermissions.Sales.Prepack, AppPermissions.Sales.AssignCustomer,
        AppPermissions.Shifts.Open, AppPermissions.Shifts.Close, AppPermissions.Shifts.View,
        AppPermissions.Customers.View, AppPermissions.Customers.ViewAll,
        AppPermissions.Customers.ReceivePayment, AppPermissions.CustomerPayments.View, AppPermissions.CustomerPayments.Create,
        AppPermissions.Returns.View, AppPermissions.Returns.Create,
        AppPermissions.TradeCases.View, AppPermissions.TradeCases.Create, AppPermissions.TradeCases.Edit,
        AppPermissions.TradeCases.Settle, AppPermissions.GoodsIssues.View, AppPermissions.GoodsIssues.Create,
        AppPermissions.GoodsIssues.Return, AppPermissions.Statements.View,
        AppPermissions.Partners.View, AppPermissions.PartnerRewards.View,
        AppPermissions.Stocks.View, AppPermissions.Branches.View, AppPermissions.Warehouses.View,
        AppPermissions.Devices.View, AppPermissions.Devices.Revoke,
        AppPermissions.Printing.ReceiptPrint, AppPermissions.Printing.ZReportPrint,
        AppPermissions.Printing.RemoteUse, AppPermissions.Printing.Host, AppPermissions.Printing.JobsViewOwn
    ];

    public static readonly string[] AgentPermissions =
    [
        AppPermissions.Products.View, AppPermissions.Categories.View, AppPermissions.Sales.View, AppPermissions.Sales.Create,
        AppPermissions.Shifts.Open, AppPermissions.Shifts.Close, AppPermissions.Shifts.View,
        AppPermissions.Customers.View, AppPermissions.Customers.Create, AppPermissions.Customers.Edit,
        AppPermissions.Customers.ReceivePayment, AppPermissions.CustomerPayments.View, AppPermissions.CustomerPayments.Create,
        AppPermissions.TradeCases.View, AppPermissions.TradeCases.Create, AppPermissions.TradeCases.Edit,
        AppPermissions.GoodsIssues.View, AppPermissions.GoodsIssues.Create, AppPermissions.GoodsIssues.Return,
        AppPermissions.Statements.View,
        AppPermissions.Partners.View, AppPermissions.Partners.Edit, AppPermissions.PartnerRewards.View,
        AppPermissions.Stocks.View, AppPermissions.StockTransfers.View, AppPermissions.StockTransfers.Receive,
        AppPermissions.Branches.View, AppPermissions.Warehouses.View,
        AppPermissions.Devices.View, AppPermissions.Devices.Revoke,
        AppPermissions.Printing.ReceiptPrint, AppPermissions.Printing.RemoteUse, AppPermissions.Printing.JobsViewOwn
    ];

    public static readonly string[] SellerAssistantPermissions =
    [
        AppPermissions.Sales.Create,
        AppPermissions.Partners.View,
        AppPermissions.Branches.View,
        AppPermissions.Warehouses.View
    ];

    public static readonly string[] CashierPermissions =
    [
        AppPermissions.Sales.Checkout, AppPermissions.Sales.View, AppPermissions.Sales.ViewAll,
        AppPermissions.Shifts.Open, AppPermissions.Shifts.Close, AppPermissions.Shifts.View,
        AppPermissions.Customers.View, AppPermissions.Customers.ViewAll, AppPermissions.Customers.ReceivePayment,
        AppPermissions.CustomerPayments.View, AppPermissions.CustomerPayments.Create,
        AppPermissions.Returns.View, AppPermissions.Returns.Create,
        AppPermissions.TradeCases.View, AppPermissions.TradeCases.Settle,
        AppPermissions.Partners.View,
        AppPermissions.Branches.View, AppPermissions.Warehouses.View,
        AppPermissions.Printing.ReceiptPrint, AppPermissions.Printing.ReceiptReprint,
        AppPermissions.Printing.ZReportPrint, AppPermissions.Printing.RemoteUse,
        AppPermissions.Printing.Host, AppPermissions.Printing.JobsViewOwn
    ];

    public static readonly string[] AccountantPermissions =
    [
        AppPermissions.Accounts.View, AppPermissions.Transactions.View,
        AppPermissions.Reports.View, AppPermissions.Reports.Export,
        AppPermissions.Sales.View, AppPermissions.Sales.ViewAll, AppPermissions.Sales.AssignCustomer,
        AppPermissions.Shifts.View, AppPermissions.Shifts.ViewAll,
        AppPermissions.Customers.View, AppPermissions.Customers.ViewAll,
        AppPermissions.CustomerPayments.View, AppPermissions.CustomerPayments.Void,
        AppPermissions.Returns.View, AppPermissions.Returns.Approve,
        AppPermissions.TradeCases.View, AppPermissions.TradeCases.Settle, AppPermissions.TradeCases.Close,
        AppPermissions.GoodsIssues.View, AppPermissions.Statements.View, AppPermissions.Statements.Export,
        AppPermissions.Partners.View, AppPermissions.PartnerRewards.View, AppPermissions.PartnerRewards.Redeem,
        AppPermissions.Suppliers.View, AppPermissions.Supplies.View,
        AppPermissions.Branches.View, AppPermissions.Warehouses.View,
        AppPermissions.Printing.DocumentPrint, AppPermissions.Printing.RemoteUse,
        AppPermissions.Printing.Host, AppPermissions.Printing.JobsViewOwn
    ];

    public static readonly string[] WarehouseOperatorPermissions =
    [
        AppPermissions.Stocks.View, AppPermissions.Stocks.Adjust,
        AppPermissions.StockTransfers.View, AppPermissions.StockTransfers.Create,
        AppPermissions.StockTransfers.Receive, AppPermissions.StockTransfers.ReceiveAny,
        AppPermissions.Branches.View, AppPermissions.Warehouses.View,
        AppPermissions.Printing.BarcodePrint, AppPermissions.Printing.RemoteUse,
        AppPermissions.Printing.Host, AppPermissions.Printing.JobsViewOwn
    ];

    public static readonly string[] SupplyOperatorPermissions =
    [
        AppPermissions.Supplies.View, AppPermissions.Supplies.Create, AppPermissions.Supplies.Edit, AppPermissions.Supplies.Import,
        AppPermissions.Suppliers.View, AppPermissions.Suppliers.Create, AppPermissions.Suppliers.Edit,
        AppPermissions.Stocks.View, AppPermissions.Branches.View, AppPermissions.Warehouses.View,
        AppPermissions.Printing.BarcodePrint, AppPermissions.Printing.RemoteUse,
        AppPermissions.Printing.Host, AppPermissions.Printing.JobsViewOwn
    ];

    private sealed record DefaultRoleSeed(
        string Name,
        string Description,
        string? StartPage,
        string? CartDestination,
        int Level,
        int Version,
        string[] Permissions);

    private static readonly DefaultRoleSeed[] DefaultBusinessRoles =
    [
        new(AppRoles.Seller, "Sotuvchi — savat va to'lov bilan to'liq savdo", "pos", "queue", AppRoles.SellerLevel, 4, SellerPermissions),
        new(AppRoles.SellerAssistant, "Sotuvchi yordamchisi — savat yig'adi va navbatga yuboradi", "pos", "queue", AppRoles.SellerAssistantLevel, 2, SellerAssistantPermissions),
        new(AppRoles.Cashier, "Kassir — navbatdagi savat uchun to'lov qabul qiladi", "pos", "queue", AppRoles.CashierLevel, 4, CashierPermissions),
        new(AppRoles.Accountant, "Hisobchi — moliya va hisobotlarni faqat ko'radi", "dashboard", null, AppRoles.AccountantLevel, 4, AccountantPermissions),
        new(AppRoles.WarehouseOperator, "Omborchi — qoldiq va ombor harakatlarini boshqaradi", "warehouse", null, AppRoles.WarehouseOperatorLevel, 2, WarehouseOperatorPermissions),
        new(AppRoles.SupplyOperator, "Kirim operatori — ta'minot va kirimni boshqaradi", "supplies", null, AppRoles.SupplyOperatorLevel, 2, SupplyOperatorPermissions),
        new(AppRoles.Agent, "Savdo agenti (mobil)", "pos", "order", AppRoles.AgentLevel, 4, AgentPermissions)
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
            .Select(u => new Unit
            {
                Name = u.Name,
                ShortName = u.ShortName,
                Dimension = u.Dimension,
                Factor = u.Factor,
                AllowFractional = u.Dimension != UnitDimension.Count,
                DefaultAllowAmountEntry = u.Dimension != UnitDimension.Count,
                IsSystem = true,
                IsDefault = u.IsDefault
            })
            .ToList();

        if (missing.Count > 0)
        {
            await context.Units.AddRangeAsync(missing);
            await context.SaveChangesAsync();
        }

        var misconfigured = await context.Units.IgnoreQueryFilters()
            .Where(u => u.Dimension == UnitDimension.Count && u.AllowFractional)
            .ToListAsync();
        if (misconfigured.Count > 0)
        {
            foreach (var unit in misconfigured) unit.AllowFractional = false;
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

        var allRoles = await context.Roles
            .Include(r => r.RolePermissions)
            .ToListAsync();

        foreach (var (legacyName, replacements) in AppPermissions.LegacyReplacements)
        {
            if (!permByName.TryGetValue(legacyName, out var legacyId))
                continue;

            var legacyGrants = await context.RolePermissions.Where(rp => rp.PermissionId == legacyId).ToListAsync();
            var roleIds = legacyGrants.Select(rp => rp.RoleId).ToHashSet();
            var pairs = (await context.RolePermissions.Where(rp => roleIds.Contains(rp.RoleId))
                .Select(rp => new { rp.RoleId, rp.PermissionId }).ToListAsync())
                .Select(x => (x.RoleId, x.PermissionId)).ToHashSet();

            foreach (var roleId in roleIds)
                foreach (var name in PermissionDependencies.Effective(replacements))
                    if (pairs.Add((roleId, permByName[name])))
                        context.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permByName[name] });

            context.RolePermissions.RemoveRange(legacyGrants);
            context.Permissions.Remove(permissions.First(p => p.Id == legacyId));
            permByName.Remove(legacyName);

            foreach (var role in allRoles.Where(r => r.GrantablePermissions.Contains(legacyName)))
                role.GrantablePermissions =
                [
                    .. role.GrantablePermissions.Where(p => p != legacyName),
                    .. PermissionDependencies.Effective(replacements)
                ];

            // Persist each legacy replacement before processing the next one so
            // shared transitive dependencies cannot be inserted twice.
            await context.SaveChangesAsync();
        }

        await context.SaveChangesAsync();

        foreach (var role in allRoles.Where(r => !r.AccessAll))
        {
            var currentNames = await context.RolePermissions
                .Where(rp => rp.RoleId == role.Id)
                .Select(rp => rp.Permission.Name)
                .ToListAsync();
            var effective = PermissionDependencies.Effective(currentNames);
            var have = (await context.RolePermissions
                .Where(rp => rp.RoleId == role.Id)
                .Select(rp => rp.PermissionId)
                .ToListAsync()).ToHashSet();
            foreach (var name in effective)
                if (permByName.TryGetValue(name, out var id) && have.Add(id))
                    context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = id });

            role.GrantablePermissions =
                [.. PermissionDependencies.Effective(role.GrantablePermissions).Where(permByName.ContainsKey)];
        }
        await context.SaveChangesAsync();

        async Task GrantAsync(string roleName, IEnumerable<string> names, string[]? grantable = null)
        {
            var role = await context.Roles.Include(r => r.RolePermissions)
                .FirstOrDefaultAsync(r => r.Name == roleName);
            if (role is null || role.AccessAll)
                return;

            var have = (await context.RolePermissions
                .Where(rp => rp.RoleId == role.Id)
                .Select(rp => rp.PermissionId)
                .ToListAsync()).ToHashSet();
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

        foreach (var definition in DefaultBusinessRoles)
        {
            var role = await context.Roles.FirstOrDefaultAsync(candidate => candidate.Name == definition.Name);
            if (role is null)
            {
                role = new Role
                {
                    Name = definition.Name,
                    Description = definition.Description,
                    StartPage = definition.StartPage,
                    CartDestination = definition.CartDestination,
                    Priority = definition.Level,
                    Level = definition.Level,
                    IsSystem = true,
                    IsActive = true,
                    TemplateVersion = definition.Version
                };
                context.Roles.Add(role);
                await context.SaveChangesAsync();
                await GrantAsync(
                    definition.Name,
                    PermissionDependencies.Effective(definition.Permissions));
            }
            else if (role.IsSystem && role.TemplateVersion < definition.Version)
            {
                await GrantAsync(
                    definition.Name,
                    PermissionDependencies.Effective(definition.Permissions));
                role.TemplateVersion = definition.Version;
            }
        }

        await GrantAsync(AppRoles.Admin, AdminGrant, AdminGrant);
        await context.SaveChangesAsync();
    }

    public static async Task SyncCurrenciesAsync(ApplicationDbContext context)
    {
        var business = await context.Businesses.FirstOrDefaultAsync();
        if (business is null)
            return;

        var existing = await context.Currencies.ToListAsync();
        var system = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [business.Currency] = "",
            ["USD"] = "AQSH dollari",
            ["EUR"] = "Yevro",
            ["RUB"] = "Rossiya rubli"
        };

        static (string Symbol, string Position, int Digits) Metadata(string code) => code switch
        {
            "UZS" => ("so'm", "Suffix", 0),
            "USD" => ("$", "Prefix", 2),
            "EUR" => ("€", "Prefix", 2),
            "RUB" => ("₽", "Suffix", 2),
            "KZT" => ("₸", "Suffix", 2),
            "TRY" => ("₺", "Prefix", 2),
            "CNY" => ("¥", "Prefix", 2),
            _ => (code, "Suffix", 2)
        };

        foreach (var (code, name) in system)
        {
            var currency = existing.FirstOrDefault(c => c.Code == code);
            if (currency is null)
            {
                currency = new Currency { Code = code, Name = name, IsSystem = true };
                context.Currencies.Add(currency);
                existing.Add(currency);
            }
            if (string.IsNullOrWhiteSpace(currency.Symbol))
            {
                var metadata = Metadata(code);
                currency.Symbol = metadata.Symbol;
                currency.SymbolPosition = metadata.Position;
                currency.DecimalDigits = metadata.Digits;
            }
        }

        foreach (var currency in existing)
            currency.IsDefault = currency.Code == business.Currency;

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

        var featureRows = await context.Features.ToListAsync();
        var existing = featureRows.Select(f => f.Code).ToHashSet();
        var legacyMulticurrency = featureRows.FirstOrDefault(f => f.Code == FeatureCatalog.Multicurrency)?.IsEnabled == true;
        var missing = FeatureCatalog.Names
            .Where(kv => !existing.Contains(kv.Key))
            .Select(kv => new Feature
            {
                Code = kv.Key,
                Name = kv.Value,
                IsEnabled = kv.Key is FeatureCatalog.PricingMulticurrency or FeatureCatalog.SalesMulticurrency
                    ? legacyMulticurrency
                    : !FeatureCatalog.DefaultDisabled.Contains(kv.Key)
            })
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
        if (developer is not null && ShouldRestoreSystemPassword(developer.PasswordHash, "developer123", verify))
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
        if (admin is not null && ShouldRestoreSystemPassword(admin.PasswordHash, "admin123", verify))
        {
            admin.PasswordHash = hashPassword(adminPassword);
            await context.SaveChangesAsync();
        }
    }

    // The configured system password may safely recover only a pristine seed
    // password or a malformed hash. Valid passwords chosen by an administrator
    // are never replaced during normal application startup.
    private static bool ShouldRestoreSystemPassword(string passwordHash, string initialPassword, Func<string, string, bool> verify)
    {
        try
        {
            return verify(initialPassword, passwordHash);
        }
        catch (Exception)
        {
            return true;
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
        var defaultRoles = DefaultBusinessRoles
            .Select(definition => new Role
            {
                Name = definition.Name,
                Description = definition.Description,
                StartPage = definition.StartPage,
                CartDestination = definition.CartDestination,
                Priority = definition.Level,
                Level = definition.Level,
                IsSystem = true,
                IsActive = true,
                TemplateVersion = definition.Version
            })
            .ToList();

        await context.Roles.AddRangeAsync([developerRole, adminRole, .. defaultRoles]);
        await context.SaveChangesAsync();

        foreach (var perm in permissions.Where(p => AdminGrant.Contains(p.Name)))
        {
            context.RolePermissions.Add(new RolePermission { RoleId = adminRole.Id, PermissionId = perm.Id });
        }

        foreach (var definition in DefaultBusinessRoles)
        {
            var role = defaultRoles.First(candidate => candidate.Name == definition.Name);
            var effective = PermissionDependencies.Effective(definition.Permissions);
            foreach (var permission in permissions.Where(candidate => effective.Contains(candidate.Name)))
                context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
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
                UserRoles = [new UserRole { RoleId = defaultRoles.First(role => role.Name == AppRoles.Seller).Id }],
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
            .Select(u => new Unit
            {
                Name = u.Name,
                ShortName = u.ShortName,
                Dimension = u.Dimension,
                Factor = u.Factor,
                AllowFractional = u.Dimension != UnitDimension.Count,
                DefaultAllowAmountEntry = u.Dimension != UnitDimension.Count,
                IsSystem = true,
                IsDefault = u.IsDefault
            })
            .ToList();
        await context.Units.AddRangeAsync(defaultUnits);

        var typeRegular = new ProductType { Name = "Oddiy mahsulot", TracksExpiry = false };
        var typeExpiring = new ProductType { Name = "Muddatli mahsulot", TracksExpiry = true };
        await context.ProductTypes.AddRangeAsync(typeRegular, typeExpiring);

        await context.SaveChangesAsync();
    }
}
