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

        var branch1 = new Branch { BusinessId = business.Id, Name = "Filial 1", Address = "Tashkent" };
        var branch2 = new Branch { BusinessId = business.Id, Name = "Filial 2", Address = "Samarqand" };
        await context.Branches.AddRangeAsync(branch1, branch2);
        await context.SaveChangesAsync();

        var warehouse = new Warehouse { BranchId = branch1.Id, Name = "Filial 1 ombori" };
        var warehouse2 = new Warehouse { BranchId = branch2.Id, Name = "Filial 2 ombori" };

        var developer = new User
        {
            FullName = "Developer",
            Username = AppRoles.Developer,
            PasswordHash = hashPassword(string.IsNullOrWhiteSpace(developerPassword) ? "developer123" : developerPassword),
            UserRoles = [new UserRole { RoleId = developerRole.Id }],
            DefaultBranchId = branch1.Id,
            IsActive = true
        };
        var admin = new User
        {
            FullName = "Administrator",
            Username = AppRoles.Admin,
            PasswordHash = hashPassword(string.IsNullOrWhiteSpace(adminPassword) ? "admin123" : adminPassword),
            UserRoles = [new UserRole { RoleId = adminRole.Id }],
            DefaultBranchId = branch1.Id,
            IsActive = true
        };
        await context.Warehouses.AddRangeAsync(warehouse, warehouse2);
        await context.Users.AddRangeAsync(developer, admin);
        await context.SaveChangesAsync();

        await context.UserBranches.AddRangeAsync(
            new UserBranch { UserId = developer.Id, BranchId = branch1.Id },
            new UserBranch { UserId = developer.Id, BranchId = branch2.Id },
            new UserBranch { UserId = admin.Id, BranchId = branch1.Id },
            new UserBranch { UserId = admin.Id, BranchId = branch2.Id });

        if (seedSeller)
        {
            var seller = new User
            {
                FullName = "Sotuvchi",
                Username = AppRoles.Seller,
                PasswordHash = hashPassword("seller123"),
                UserRoles = [new UserRole { RoleId = sellerRole.Id }],
                DefaultBranchId = branch1.Id,
                IsActive = true
            };
            await context.Users.AddAsync(seller);
            await context.SaveChangesAsync();
            await context.UserBranches.AddAsync(new UserBranch { UserId = seller.Id, BranchId = branch1.Id });
        }

        var shopCashAccount = new Account { BranchId = branch1.Id, Name = "Naqd kassa", Type = AccountType.Cash, Balance = 0 };
        var shopCardAccount = new Account { BranchId = branch1.Id, Name = "Bank karta", Type = AccountType.Card, Balance = 0 };
        var branch2CashAccount = new Account { BranchId = branch2.Id, Name = "Naqd kassa", Type = AccountType.Cash, Balance = 0 };
        await context.Accounts.AddRangeAsync(shopCashAccount, shopCardAccount, branch2CashAccount);

        var defaultUnits = SystemUnits
            .Select(u => new Unit { Name = u.Name, ShortName = u.ShortName, Dimension = u.Dimension, Factor = u.Factor, IsSystem = true, IsDefault = u.IsDefault })
            .ToList();
        await context.Units.AddRangeAsync(defaultUnits);

        await context.SaveChangesAsync();

        var dona = defaultUnits.First(u => u.ShortName == "dona");
        var metr = defaultUnits.First(u => u.ShortName == "m");

        var catMixers = new Category { Name = "Smesitellar" };
        var catPipes = new Category { Name = "Trubalar va fitinglar" };
        var catValves = new Category { Name = "Kranlar va ventillar" };
        var catSewage = new Category { Name = "Kanalizatsiya" };
        var catFixtures = new Category { Name = "Santexnika jihozlari" };
        var catSealants = new Category { Name = "Germetik va yelimlar" };

        var catHeating = new Category { Name = "Isitish" };

        await context.Categories.AddRangeAsync(catMixers, catPipes, catValves, catSewage, catFixtures, catSealants, catHeating);

        var typeRegular = new ProductType { Name = "Oddiy mahsulot", TracksExpiry = false };
        var typeExpiring = new ProductType { Name = "Muddatli mahsulot", TracksExpiry = true };
        await context.ProductTypes.AddRangeAsync(typeRegular, typeExpiring);
        await context.SaveChangesAsync();

        var products = new List<Product>
        {
            new() { Name = "Smesitel oshxona Zegor", CategoryId = catMixers.Id, UnitId = dona.Id, MinStock = 3, ImageKey = "seed/p00.jpg" },
            new() { Name = "Smesitel vanna Mixxus", CategoryId = catMixers.Id, UnitId = dona.Id, MinStock = 3, ImageKey = "seed/p01.jpg" },
            new() { Name = "Smesitel rakovina Haiba", CategoryId = catMixers.Id, UnitId = dona.Id, MinStock = 3, ImageKey = "seed/p02.jpg" },
            new() { Name = "PPR truba 20mm PN20", CategoryId = catPipes.Id, UnitId = metr.Id, MinStock = 50, ImageKey = "seed/p03.jpg" },
            new() { Name = "PPR truba 25mm PN20", CategoryId = catPipes.Id, UnitId = metr.Id, MinStock = 50, ImageKey = "seed/p04.jpg" },
            new() { Name = "PPR truba 32mm PN20", CategoryId = catPipes.Id, UnitId = metr.Id, MinStock = 30, ImageKey = "seed/p05.jpg" },
            new() { Name = "Mufta PPR 20mm", CategoryId = catPipes.Id, UnitId = dona.Id, MinStock = 40, ImageKey = "seed/p06.jpg" },
            new() { Name = "Burchak PPR 20mm 90", CategoryId = catPipes.Id, UnitId = dona.Id, MinStock = 40 },
            new() { Name = "Trojnik PPR 25mm", CategoryId = catPipes.Id, UnitId = dona.Id, MinStock = 30, ImageKey = "seed/p08.jpg" },
            new() { Name = "Amerikanka PPR 20mm", CategoryId = catPipes.Id, UnitId = dona.Id, MinStock = 20 },
            new() { Name = "Sharli kran 1/2 Itap", CategoryId = catValves.Id, UnitId = dona.Id, MinStock = 10, ImageKey = "seed/p10.jpg" },
            new() { Name = "Sharli kran 3/4 Itap", CategoryId = catValves.Id, UnitId = dona.Id, MinStock = 10, ImageKey = "seed/p11.jpg" },
            new() { Name = "Radiator ventili 1/2", CategoryId = catValves.Id, UnitId = dona.Id, MinStock = 8, ImageKey = "seed/p12.jpg" },
            new() { Name = "Sifon rakovina uchun", CategoryId = catSewage.Id, UnitId = dona.Id, MinStock = 8, ImageKey = "seed/p13.jpg" },
            new() { Name = "Gofra unitaz uchun", CategoryId = catSewage.Id, UnitId = dona.Id, MinStock = 6, ImageKey = "seed/p14.jpg" },
            new() { Name = "Kanalizatsiya quvuri 50mm 2m", CategoryId = catSewage.Id, UnitId = dona.Id, MinStock = 15, ImageKey = "seed/p15.jpg" },
            new() { Name = "Kanalizatsiya quvuri 110mm 2m", CategoryId = catSewage.Id, UnitId = dona.Id, MinStock = 10, ImageKey = "seed/p16.jpg" },
            new() { Name = "Kanalizatsiya burchagi 50mm 45", CategoryId = catSewage.Id, UnitId = dona.Id, MinStock = 20 },
            new() { Name = "Unitaz o'rindig'i universal", CategoryId = catFixtures.Id, UnitId = dona.Id, MinStock = 4, ImageKey = "seed/p18.jpg" },
            new() { Name = "Rakovina keramik oq", CategoryId = catFixtures.Id, UnitId = dona.Id, MinStock = 2, ImageKey = "seed/p19.jpg" },
            new() { Name = "Dush lednika 5 rejimli", CategoryId = catFixtures.Id, UnitId = dona.Id, MinStock = 6, ImageKey = "seed/p20.jpg" },
            new() { Name = "Dush shlangi 1.5m", CategoryId = catFixtures.Id, UnitId = dona.Id, MinStock = 8, ImageKey = "seed/p21.jpg" },
            new() { Name = "Suv shlangi 1/2 60sm juft", CategoryId = catFixtures.Id, UnitId = dona.Id, MinStock = 12, ImageKey = "seed/p22.jpg" },
            new() { Name = "Unitaz armaturasi to'plam", CategoryId = catFixtures.Id, UnitId = dona.Id, MinStock = 4 },
            new() { Name = "FUM lenta 19mm", CategoryId = catSealants.Id, UnitId = dona.Id, MinStock = 30, ImageKey = "seed/p24.jpg" },
            new() { Name = "Len tolasi 100g", CategoryId = catSealants.Id, UnitId = dona.Id, MinStock = 15 },
            new() { Name = "Silikon germetik sanitar 280ml", CategoryId = catSealants.Id, UnitId = dona.Id, MinStock = 10, ImageKey = "seed/p26.jpg" },
            new() { Name = "PVX yelim 250ml", CategoryId = catSealants.Id, UnitId = dona.Id, MinStock = 8 },
            new() { Name = "Radiator alyuminiy seksiya", CategoryId = catHeating.Id, UnitId = dona.Id, MinStock = 10, ImageKey = "seed/p28.jpg" },
            new() { Name = "TEN 1.5kVt suv isitgich uchun", CategoryId = catHeating.Id, UnitId = dona.Id, MinStock = 4, ImageKey = "seed/p29.jpg" },
            new() { Name = "Sirkulyatsion nasos 25-40", CategoryId = catHeating.Id, UnitId = dona.Id, MinStock = 2, ImageKey = "seed/p30.jpg" },
            new() { Name = "Suv hisoblagichi DN15", CategoryId = catHeating.Id, UnitId = dona.Id, MinStock = 4, ImageKey = "seed/p31.jpg" },
            new() { Name = "O'tish muftasi 1/2x3/4", CategoryId = catPipes.Id, UnitId = dona.Id, MinStock = 20, ImageKey = "seed/p32.jpg" },
        };

        var expiryIdx = new[] { 26, 27 };
        for (var i = 0; i < products.Count; i++)
            products[i].ProductTypeId = expiryIdx.Contains(i) ? typeExpiring.Id : typeRegular.Id;
        products[0].Attributes = """{"brend":"Zegor","turi":"oshxona"}""";

        await context.Products.AddRangeAsync(products);
        await context.SaveChangesAsync();

        var variants = products.Select(p => new ProductVariant { ProductId = p.Id, IsDefault = true }).ToList();
        await context.ProductVariants.AddRangeAsync(variants);
        await context.SaveChangesAsync();

        var sellingPrices = new[]
        {
            385000m, 520000m, 295000m, 9500m, 14000m, 22000m, 1500m, 1800m, 3500m, 12000m, 38000m, 52000m,
            45000m, 35000m, 48000m, 28000m, 65000m, 6500m, 95000m, 420000m, 55000m, 40000m, 18000m, 85000m,
            4000m, 8000m, 42000m, 55000m, 115000m, 120000m, 950000m, 185000m, 9000m
        };
        for (var i = 0; i < products.Count; i++)
            await context.ProductPrices.AddAsync(new ProductPrice { VariantId = variants[i].Id, SellingPrice = sellingPrices[i] });
        await context.SaveChangesAsync();

        var barcodes = new List<Barcode>
        {
            new() { VariantId = variants[0].Id, Code = "5449000214911" },
            new() { VariantId = variants[1].Id, Code = "4600494600012" },
            new() { VariantId = variants[2].Id, Code = "5449000011527" },
            new() { VariantId = variants[3].Id, Code = "4780001000011" },
            new() { VariantId = variants[4].Id, Code = "4780001000028" },
            new() { VariantId = variants[5].Id, Code = "4780001000035" },
            new() { VariantId = variants[6].Id, Code = "4780002000017" },
            new() { VariantId = variants[7].Id, Code = "4780002000024" },
            new() { VariantId = variants[8].Id, Code = "4780002000031" },
            new() { VariantId = variants[9].Id, Code = "4780003000016" },
            new() { VariantId = variants[10].Id, Code = "4780003000023" },
            new() { VariantId = variants[11].Id, Code = "4780003000030" },
            new() { VariantId = variants[12].Id, Code = "4780003000047" },
            new() { VariantId = variants[13].Id, Code = "4780004000015" },
            new() { VariantId = variants[14].Id, Code = "4780004000022" },
            new() { VariantId = variants[15].Id, Code = "4780005000014" },
            new() { VariantId = variants[16].Id, Code = "4780002000048" },
            new() { VariantId = variants[17].Id, Code = "4780001000042" },
            new() { VariantId = variants[18].Id, Code = "4780001000059" },
            new() { VariantId = variants[19].Id, Code = "4780006000013" },
            new() { VariantId = variants[20].Id, Code = "4780006000020" },
            new() { VariantId = variants[21].Id, Code = "4780006000037" },
            new() { VariantId = variants[22].Id, Code = "4780006000044" },
            new() { VariantId = variants[23].Id, Code = "4780004000039" },
            new() { VariantId = variants[24].Id, Code = "4780004000046" },
            new() { VariantId = variants[25].Id, Code = "4780003000054" },
            new() { VariantId = variants[26].Id, Code = "4780003000061" },
            new() { VariantId = variants[27].Id, Code = "4780003000078" },
            new() { VariantId = variants[28].Id, Code = "4780003000085" },
            new() { VariantId = variants[29].Id, Code = "4780002000055" },
            new() { VariantId = variants[30].Id, Code = "4780003000092" },
            new() { VariantId = variants[31].Id, Code = "4780002000062" },
            new() { VariantId = variants[32].Id, Code = "4780005000038" },
        };

        await context.Barcodes.AddRangeAsync(barcodes);

        var stocks = new List<Stock>
        {
            new() { VariantId = variants[0].Id, WarehouseId = warehouse.Id, Quantity = 15, PurchasePrice = 265000 },
            new() { VariantId = variants[1].Id, WarehouseId = warehouse.Id, Quantity = 6, PurchasePrice = 360000 },
            new() { VariantId = variants[2].Id, WarehouseId = warehouse.Id, Quantity = 10, PurchasePrice = 205000 },
            new() { VariantId = variants[3].Id, WarehouseId = warehouse.Id, Quantity = 200, PurchasePrice = 6200 },
            new() { VariantId = variants[4].Id, WarehouseId = warehouse.Id, Quantity = 160, PurchasePrice = 9100 },
            new() { VariantId = variants[5].Id, WarehouseId = warehouse.Id, Quantity = 120, PurchasePrice = 14300 },
            new() { VariantId = variants[6].Id, WarehouseId = warehouse.Id, Quantity = 150, PurchasePrice = 900 },
            new() { VariantId = variants[7].Id, WarehouseId = warehouse.Id, Quantity = 140, PurchasePrice = 1100 },
            new() { VariantId = variants[8].Id, WarehouseId = warehouse.Id, Quantity = 100, PurchasePrice = 2200 },
            new() { VariantId = variants[9].Id, WarehouseId = warehouse.Id, Quantity = 80, PurchasePrice = 7800 },
            new() { VariantId = variants[10].Id, WarehouseId = warehouse.Id, Quantity = 35, PurchasePrice = 26000 },
            new() { VariantId = variants[11].Id, WarehouseId = warehouse.Id, Quantity = 30, PurchasePrice = 36000 },
            new() { VariantId = variants[12].Id, WarehouseId = warehouse.Id, Quantity = 25, PurchasePrice = 31000 },
            new() { VariantId = variants[13].Id, WarehouseId = warehouse.Id, Quantity = 30, PurchasePrice = 23000 },
            new() { VariantId = variants[14].Id, WarehouseId = warehouse.Id, Quantity = 20, PurchasePrice = 33000 },
            new() { VariantId = variants[15].Id, WarehouseId = warehouse.Id, Quantity = 60, PurchasePrice = 18500 },
            new() { VariantId = variants[16].Id, WarehouseId = warehouse.Id, Quantity = 30, PurchasePrice = 44000 },
            new() { VariantId = variants[17].Id, WarehouseId = warehouse.Id, Quantity = 90, PurchasePrice = 4200 },
            new() { VariantId = variants[18].Id, WarehouseId = warehouse.Id, Quantity = 20, PurchasePrice = 64000 },
            new() { VariantId = variants[19].Id, WarehouseId = warehouse.Id, Quantity = 5, PurchasePrice = 290000 },
            new() { VariantId = variants[20].Id, WarehouseId = warehouse.Id, Quantity = 30, PurchasePrice = 37000 },
            new() { VariantId = variants[21].Id, WarehouseId = warehouse.Id, Quantity = 35, PurchasePrice = 27000 },
            new() { VariantId = variants[22].Id, WarehouseId = warehouse.Id, Quantity = 40, PurchasePrice = 11500 },
            new() { VariantId = variants[23].Id, WarehouseId = warehouse.Id, Quantity = 20, PurchasePrice = 58000 },
            new() { VariantId = variants[24].Id, WarehouseId = warehouse.Id, Quantity = 150, PurchasePrice = 2400 },
            new() { VariantId = variants[25].Id, WarehouseId = warehouse.Id, Quantity = 60, PurchasePrice = 5100 },
            new() { VariantId = variants[26].Id, WarehouseId = warehouse.Id, Quantity = 40, PurchasePrice = 28000 },
            new() { VariantId = variants[27].Id, WarehouseId = warehouse.Id, Quantity = 25, PurchasePrice = 37500 },
            new() { VariantId = variants[28].Id, WarehouseId = warehouse.Id, Quantity = 15, PurchasePrice = 82000 },
            new() { VariantId = variants[29].Id, WarehouseId = warehouse.Id, Quantity = 20, PurchasePrice = 82000 },
            new() { VariantId = variants[30].Id, WarehouseId = warehouse.Id, Quantity = 5, PurchasePrice = 680000 },
            new() { VariantId = variants[31].Id, WarehouseId = warehouse.Id, Quantity = 15, PurchasePrice = 128000 },
            new() { VariantId = variants[32].Id, WarehouseId = warehouse.Id, Quantity = 90, PurchasePrice = 5600 },
        };

        foreach (var s in stocks) s.BranchId = branch1.Id;
        await context.Stocks.AddRangeAsync(stocks);

        var branch2Stocks = new List<Stock>
        {
            new() { BranchId = branch2.Id, VariantId = variants[0].Id, WarehouseId = warehouse2.Id, Quantity = 4, PurchasePrice = 265000 },
            new() { BranchId = branch2.Id, VariantId = variants[3].Id, WarehouseId = warehouse2.Id, Quantity = 80, PurchasePrice = 6200 },
            new() { BranchId = branch2.Id, VariantId = variants[9].Id, WarehouseId = warehouse2.Id, Quantity = 30, PurchasePrice = 7800 },
        };
        await context.Stocks.AddRangeAsync(branch2Stocks);

        await context.SaveChangesAsync();
    }
}
