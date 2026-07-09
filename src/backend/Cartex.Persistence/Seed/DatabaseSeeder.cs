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
        AppPermissions.Shifts.Manage, AppPermissions.Shifts.View, AppPermissions.Customers.View,
        AppPermissions.Stocks.View, AppPermissions.Branches.View, AppPermissions.Warehouses.View,
        AppPermissions.Devices.Manage
    ];

    public static readonly string[] AgentPermissions =
    [
        AppPermissions.Products.View, AppPermissions.Sales.View, AppPermissions.Sales.Create,
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
            context.Roles.Add(new Role { Name = AppRoles.Agent, Description = "Dala agenti (mobil savdo)", StartPage = "pos", Priority = AppRoles.AgentLevel, Level = AppRoles.AgentLevel, IsSystem = true });
            await context.SaveChangesAsync();
        }

        await GrantAsync(AppRoles.Admin, AdminGrant, AdminGrant);
        await GrantAsync(AppRoles.Seller, SellerPermissions);
        await GrantAsync(AppRoles.Agent, AgentPermissions);
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

    public static async Task SeedAsync(ApplicationDbContext context, Func<string, string> hashPassword, string? developerPassword = null)
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
        var sellerRole = new Role { Name = AppRoles.Seller, Description = "Sotuvchi (kassa)", StartPage = "pos", Priority = AppRoles.SellerLevel, Level = AppRoles.SellerLevel, IsSystem = true };
        var agentRole = new Role { Name = AppRoles.Agent, Description = "Dala agenti (mobil savdo)", StartPage = "pos", Priority = AppRoles.AgentLevel, Level = AppRoles.AgentLevel, IsSystem = true };

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
            PasswordHash = hashPassword("admin123"),
            UserRoles = [new UserRole { RoleId = adminRole.Id }],
            DefaultBranchId = branch1.Id,
            IsActive = true
        };
        var seller = new User
        {
            FullName = "Muqimjon Mamadaliyev",
            Username = AppRoles.Seller,
            PasswordHash = hashPassword("seller123"),
            UserRoles = [new UserRole { RoleId = sellerRole.Id }],
            DefaultBranchId = branch1.Id,
            IsActive = true
        };
        await context.Warehouses.AddRangeAsync(warehouse, warehouse2);
        await context.Users.AddRangeAsync(developer, admin, seller);
        await context.SaveChangesAsync();

        await context.UserBranches.AddRangeAsync(
            new UserBranch { UserId = developer.Id, BranchId = branch1.Id },
            new UserBranch { UserId = developer.Id, BranchId = branch2.Id },
            new UserBranch { UserId = admin.Id, BranchId = branch1.Id },
            new UserBranch { UserId = admin.Id, BranchId = branch2.Id },
            new UserBranch { UserId = seller.Id, BranchId = branch1.Id });

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
        var kg = defaultUnits.First(u => u.ShortName == "kg");
        var litr = defaultUnits.First(u => u.ShortName == "l");

        var catFood = new Category { Name = "Oziq-ovqat" };
        var catBeverages = new Category { Name = "Ichimliklar" };
        var catDairy = new Category { Name = "Sut mahsulotlari" };
        var catBakery = new Category { Name = "Non mahsulotlari" };
        var catHousehold = new Category { Name = "Uy-ro'zg'or" };
        var catPersonalCare = new Category { Name = "Shaxsiy gigiyena" };

        var catSnacks = new Category { Name = "Konditeriya" };

        await context.Categories.AddRangeAsync(catFood, catBeverages, catDairy, catBakery, catHousehold, catPersonalCare, catSnacks);

        var typeFood = new ProductType { Name = "Oziq-ovqat", TracksExpiry = true };
        var typeWeighed = new ProductType { Name = "Tarozili mahsulot", TracksExpiry = true };
        var typeNonFood = new ProductType { Name = "Nooziq-ovqat", TracksExpiry = false };
        await context.ProductTypes.AddRangeAsync(typeFood, typeWeighed, typeNonFood);
        await context.SaveChangesAsync();

        var products = new List<Product>
        {
            new() { Name = "Coca-Cola 1.5L", CategoryId = catBeverages.Id, UnitId = dona.Id, MinStock = 5 },
            new() { Name = "Pepsi 1L", CategoryId = catBeverages.Id, UnitId = dona.Id, MinStock = 5 },
            new() { Name = "Fanta 0.5L", CategoryId = catBeverages.Id, UnitId = dona.Id, MinStock = 5 },
            new() { Name = "Non", CategoryId = catBakery.Id, UnitId = dona.Id, MinStock = 10 },
            new() { Name = "Bulka", CategoryId = catBakery.Id, UnitId = dona.Id, MinStock = 10 },
            new() { Name = "Lavash", CategoryId = catBakery.Id, UnitId = dona.Id, MinStock = 10 },
            new() { Name = "Sut 1L", CategoryId = catDairy.Id, UnitId = dona.Id, MinStock = 5 },
            new() { Name = "Qatiq 0.5L", CategoryId = catDairy.Id, UnitId = dona.Id, MinStock = 5 },
            new() { Name = "Tvorog 200g", CategoryId = catDairy.Id, UnitId = dona.Id, MinStock = 5 },
            new() { Name = "Guruch 1kg", CategoryId = catFood.Id, UnitId = kg.Id, MinStock = 10 },
            new() { Name = "Shakar 1kg", CategoryId = catFood.Id, UnitId = kg.Id, MinStock = 10 },
            new() { Name = "Un 2kg", CategoryId = catFood.Id, UnitId = kg.Id, MinStock = 10 },
            new() { Name = "Yog' 1L", CategoryId = catFood.Id, UnitId = litr.Id, MinStock = 5 },
            new() { Name = "Sabun", CategoryId = catHousehold.Id, UnitId = dona.Id, MinStock = 5 },
            new() { Name = "Shampun", CategoryId = catPersonalCare.Id, UnitId = dona.Id, MinStock = 5 },
            new() { Name = "Kolbasa", CategoryId = catFood.Id, UnitId = dona.Id, MinStock = 5 },
            new() { Name = "Smetana 400g", CategoryId = catDairy.Id, UnitId = dona.Id, MinStock = 5 },
            new() { Name = "Oq non", CategoryId = catBakery.Id, UnitId = dona.Id, MinStock = 10 },
            new() { Name = "Patir non", CategoryId = catBakery.Id, UnitId = dona.Id, MinStock = 10 },
            new() { Name = "Chips Lays 150g", CategoryId = catSnacks.Id, UnitId = dona.Id, MinStock = 5 },
            new() { Name = "Shokolad Alpen Gold", CategoryId = catSnacks.Id, UnitId = dona.Id, MinStock = 5 },
            new() { Name = "Konfet Karakum 1kg", CategoryId = catSnacks.Id, UnitId = kg.Id, MinStock = 5 },
            new() { Name = "Pechenye Yubileynoe", CategoryId = catSnacks.Id, UnitId = dona.Id, MinStock = 5 },
            new() { Name = "Tish pastasi Colgate", CategoryId = catPersonalCare.Id, UnitId = dona.Id, MinStock = 5 },
            new() { Name = "Sovun Safeguard", CategoryId = catPersonalCare.Id, UnitId = dona.Id, MinStock = 5 },
            new() { Name = "Makaron 400g", CategoryId = catFood.Id, UnitId = dona.Id, MinStock = 10 },
            new() { Name = "Tuz 1kg", CategoryId = catFood.Id, UnitId = kg.Id, MinStock = 10 },
            new() { Name = "Sirka 0.5L", CategoryId = catFood.Id, UnitId = dona.Id, MinStock = 5 },
            new() { Name = "Pomidor pastasi 200g", CategoryId = catFood.Id, UnitId = dona.Id, MinStock = 5 },
            new() { Name = "Sprite 1L", CategoryId = catBeverages.Id, UnitId = dona.Id, MinStock = 5 },
            new() { Name = "Choy Tess 100p", CategoryId = catBeverages.Id, UnitId = dona.Id, MinStock = 5 },
            new() { Name = "Kofe Nescafe 3v1", CategoryId = catBeverages.Id, UnitId = dona.Id, MinStock = 5 },
            new() { Name = "Tovuq go'shti 1kg", CategoryId = catFood.Id, UnitId = kg.Id, MinStock = 5 },
        };

        var weighedIdx = new[] { 9, 10, 11, 21, 26, 32 };
        var nonFoodIdx = new[] { 13, 14, 23, 24 };
        for (var i = 0; i < products.Count; i++)
            products[i].ProductTypeId = weighedIdx.Contains(i) ? typeWeighed.Id
                : nonFoodIdx.Contains(i) ? typeNonFood.Id
                : typeFood.Id;
        products[0].Attributes = """{"hajm":"1.5L","brend":"Coca-Cola"}""";

        await context.Products.AddRangeAsync(products);
        await context.SaveChangesAsync();

        var variants = products.Select(p => new ProductVariant { ProductId = p.Id, IsDefault = true }).ToList();
        await context.ProductVariants.AddRangeAsync(variants);
        await context.SaveChangesAsync();

        var sellingPrices = new[]
        {
            10000m, 7500m, 5000m, 4000m, 4500m, 5000m, 10000m, 7500m, 9000m, 15000m, 12000m, 17000m,
            22000m, 6500m, 19000m, 18000m, 8000m, 5000m, 6000m, 7000m, 10000m, 20000m, 9000m, 12000m,
            5500m, 6000m, 4000m, 7000m, 5500m, 7500m, 12000m, 9000m, 25000m
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
            new() { VariantId = variants[0].Id, WarehouseId = warehouse.Id, Quantity = 120, PurchasePrice = 8000 },
            new() { VariantId = variants[1].Id, WarehouseId = warehouse.Id, Quantity = 100, PurchasePrice = 6000 },
            new() { VariantId = variants[2].Id, WarehouseId = warehouse.Id, Quantity = 150, PurchasePrice = 4000 },
            new() { VariantId = variants[3].Id, WarehouseId = warehouse.Id, Quantity = 80, PurchasePrice = 3000 },
            new() { VariantId = variants[4].Id, WarehouseId = warehouse.Id, Quantity = 60, PurchasePrice = 3500 },
            new() { VariantId = variants[5].Id, WarehouseId = warehouse.Id, Quantity = 50, PurchasePrice = 4000 },
            new() { VariantId = variants[6].Id, WarehouseId = warehouse.Id, Quantity = 90, PurchasePrice = 8000 },
            new() { VariantId = variants[7].Id, WarehouseId = warehouse.Id, Quantity = 70, PurchasePrice = 6000 },
            new() { VariantId = variants[8].Id, WarehouseId = warehouse.Id, Quantity = 40, PurchasePrice = 7000 },
            new() { VariantId = variants[9].Id, WarehouseId = warehouse.Id, Quantity = 200, PurchasePrice = 12000 },
            new() { VariantId = variants[10].Id, WarehouseId = warehouse.Id, Quantity = 180, PurchasePrice = 10000 },
            new() { VariantId = variants[11].Id, WarehouseId = warehouse.Id, Quantity = 100, PurchasePrice = 14000 },
            new() { VariantId = variants[12].Id, WarehouseId = warehouse.Id, Quantity = 60, PurchasePrice = 18000 },
            new() { VariantId = variants[13].Id, WarehouseId = warehouse.Id, Quantity = 45, PurchasePrice = 5000 },
            new() { VariantId = variants[14].Id, WarehouseId = warehouse.Id, Quantity = 30, PurchasePrice = 15000 },
            new() { VariantId = variants[15].Id, WarehouseId = warehouse.Id, Quantity = 3, PurchasePrice = 15000 },
            new() { VariantId = variants[16].Id, WarehouseId = warehouse.Id, Quantity = 25, PurchasePrice = 6000 },
            new() { VariantId = variants[17].Id, WarehouseId = warehouse.Id, Quantity = 40, PurchasePrice = 3500 },
            new() { VariantId = variants[18].Id, WarehouseId = warehouse.Id, Quantity = 35, PurchasePrice = 4500 },
            new() { VariantId = variants[19].Id, WarehouseId = warehouse.Id, Quantity = 4, PurchasePrice = 5500 },
            new() { VariantId = variants[20].Id, WarehouseId = warehouse.Id, Quantity = 0, PurchasePrice = 8000 },
            new() { VariantId = variants[21].Id, WarehouseId = warehouse.Id, Quantity = 15, PurchasePrice = 16000 },
            new() { VariantId = variants[22].Id, WarehouseId = warehouse.Id, Quantity = 2, PurchasePrice = 7000 },
            new() { VariantId = variants[23].Id, WarehouseId = warehouse.Id, Quantity = 20, PurchasePrice = 9000 },
            new() { VariantId = variants[24].Id, WarehouseId = warehouse.Id, Quantity = 0, PurchasePrice = 4000 },
            new() { VariantId = variants[25].Id, WarehouseId = warehouse.Id, Quantity = 55, PurchasePrice = 4500 },
            new() { VariantId = variants[26].Id, WarehouseId = warehouse.Id, Quantity = 3, PurchasePrice = 3000 },
            new() { VariantId = variants[27].Id, WarehouseId = warehouse.Id, Quantity = 1, PurchasePrice = 5000 },
            new() { VariantId = variants[28].Id, WarehouseId = warehouse.Id, Quantity = 0, PurchasePrice = 4000 },
            new() { VariantId = variants[29].Id, WarehouseId = warehouse.Id, Quantity = 65, PurchasePrice = 5500 },
            new() { VariantId = variants[30].Id, WarehouseId = warehouse.Id, Quantity = 30, PurchasePrice = 9000 },
            new() { VariantId = variants[31].Id, WarehouseId = warehouse.Id, Quantity = 4, PurchasePrice = 7000 },
            new() { VariantId = variants[32].Id, WarehouseId = warehouse.Id, Quantity = 20, PurchasePrice = 20000 },
        };

        foreach (var s in stocks) s.BranchId = branch1.Id;
        await context.Stocks.AddRangeAsync(stocks);

        var branch2Stocks = new List<Stock>
        {
            new() { BranchId = branch2.Id, VariantId = variants[0].Id, WarehouseId = warehouse2.Id, Quantity = 50, PurchasePrice = 8000 },
            new() { BranchId = branch2.Id, VariantId = variants[3].Id, WarehouseId = warehouse2.Id, Quantity = 40, PurchasePrice = 3000 },
            new() { BranchId = branch2.Id, VariantId = variants[9].Id, WarehouseId = warehouse2.Id, Quantity = 30, PurchasePrice = 12000 },
        };
        await context.Stocks.AddRangeAsync(branch2Stocks);

        await context.SaveChangesAsync();
    }
}
