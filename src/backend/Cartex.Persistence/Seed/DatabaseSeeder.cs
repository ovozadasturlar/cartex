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
            new() { Name = "branches.view", Description = "View branches" },
            new() { Name = "branches.manage", Description = "Create/edit branches" },
            new() { Name = "branch.viewAll", Description = "Access data of all branches" },
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
            new() { Name = "loyalty.view", Description = "View loyalty/cashback settings" },
            new() { Name = "loyalty.manage", Description = "Manage loyalty/cashback settings" },
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

        var managerPermissions = permissions.Where(p => !p.Name.Contains("roles.manage") && !p.Name.Contains("branches.manage")).ToList();
        foreach (var perm in managerPermissions)
        {
            context.RolePermissions.Add(new RolePermission { RoleId = managerRole.Id, PermissionId = perm.Id });
        }

        await context.SaveChangesAsync();

        var business = new Business { Name = "Cartex Biznes" };
        await context.Businesses.AddAsync(business);
        await context.SaveChangesAsync();

        await context.LoyaltyPrograms.AddAsync(new LoyaltyProgram { IsEnabled = true, Base = CashbackBase.PercentOfTotal, TotalPercent = 1 });

        var branch1 = new Branch { BusinessId = business.Id, Name = "Filial 1", Address = "Tashkent" };
        var branch2 = new Branch { BusinessId = business.Id, Name = "Filial 2", Address = "Samarqand" };
        await context.Branches.AddRangeAsync(branch1, branch2);
        await context.SaveChangesAsync();

        var warehouse = new Warehouse { BranchId = branch1.Id, Name = "Filial 1 ombori" };
        var warehouse2 = new Warehouse { BranchId = branch2.Id, Name = "Filial 2 ombori" };

        var admin = new User
        {
            FullName = "Administrator",
            Username = "admin",
            PasswordHash = hashPassword("admin123"),
            RoleId = adminRole.Id,
            DefaultBranchId = branch1.Id,
            IsActive = true
        };
        var cashier = new User
        {
            FullName = "Kassir (Filial 1)",
            Username = "cashier",
            PasswordHash = hashPassword("cashier123"),
            RoleId = cashierRole.Id,
            DefaultBranchId = branch1.Id,
            IsActive = true
        };
        await context.Warehouses.AddRangeAsync(warehouse, warehouse2);
        await context.Users.AddRangeAsync(admin, cashier);
        await context.SaveChangesAsync();

        await context.UserBranches.AddRangeAsync(
            new UserBranch { UserId = admin.Id, BranchId = branch1.Id },
            new UserBranch { UserId = admin.Id, BranchId = branch2.Id },
            new UserBranch { UserId = cashier.Id, BranchId = branch1.Id });

        var shopCashAccount = new Account { BranchId = branch1.Id, Name = "Naqd kassa", Type = AccountType.Cash, Balance = 0 };
        var shopCardAccount = new Account { BranchId = branch1.Id, Name = "Bank karta", Type = AccountType.Card, Balance = 0 };
        var branch2CashAccount = new Account { BranchId = branch2.Id, Name = "Naqd kassa", Type = AccountType.Cash, Balance = 0 };
        await context.Accounts.AddRangeAsync(shopCashAccount, shopCardAccount, branch2CashAccount);

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

        var dona = defaultUnits[0];
        var kg = defaultUnits[1];
        var litr = defaultUnits[2];

        var catFood = new Category { Name = "Oziq-ovqat" };
        var catBeverages = new Category { Name = "Ichimliklar" };
        var catDairy = new Category { Name = "Sut mahsulotlari" };
        var catBakery = new Category { Name = "Non mahsulotlari" };
        var catHousehold = new Category { Name = "Uy-ro'zg'or" };
        var catPersonalCare = new Category { Name = "Shaxsiy gigiyena" };

        var catSnacks = new Category { Name = "Konditeriya" };

        await context.Categories.AddRangeAsync(catFood, catBeverages, catDairy, catBakery, catHousehold, catPersonalCare, catSnacks);

        var typeFood = new ProductType { Name = "Oziq-ovqat", TracksExpiry = true, MeasureMode = MeasureMode.Counted };
        var typeWeighed = new ProductType { Name = "Tarozili mahsulot", TracksExpiry = true, MeasureMode = MeasureMode.Weighed };
        var typeNonFood = new ProductType { Name = "Nooziq-ovqat", TracksExpiry = false, MeasureMode = MeasureMode.Counted };
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

        var sellingPrices = new[]
        {
            10000m, 7500m, 5000m, 4000m, 4500m, 5000m, 10000m, 7500m, 9000m, 15000m, 12000m, 17000m,
            22000m, 6500m, 19000m, 18000m, 8000m, 5000m, 6000m, 7000m, 10000m, 20000m, 9000m, 12000m,
            5500m, 6000m, 4000m, 7000m, 5500m, 7500m, 12000m, 9000m, 25000m
        };
        for (var i = 0; i < products.Count; i++)
            await context.ProductPrices.AddAsync(new ProductPrice { ProductId = products[i].Id, SellingPrice = sellingPrices[i] });
        await context.SaveChangesAsync();

        var barcodes = new List<Barcode>
        {
            new() { ProductId = products[0].Id, Code = "5449000214911" },
            new() { ProductId = products[1].Id, Code = "4600494600012" },
            new() { ProductId = products[2].Id, Code = "5449000011527" },
            new() { ProductId = products[3].Id, Code = "4780001000011" },
            new() { ProductId = products[4].Id, Code = "4780001000028" },
            new() { ProductId = products[5].Id, Code = "4780001000035" },
            new() { ProductId = products[6].Id, Code = "4780002000017" },
            new() { ProductId = products[7].Id, Code = "4780002000024" },
            new() { ProductId = products[8].Id, Code = "4780002000031" },
            new() { ProductId = products[9].Id, Code = "4780003000016" },
            new() { ProductId = products[10].Id, Code = "4780003000023" },
            new() { ProductId = products[11].Id, Code = "4780003000030" },
            new() { ProductId = products[12].Id, Code = "4780003000047" },
            new() { ProductId = products[13].Id, Code = "4780004000015" },
            new() { ProductId = products[14].Id, Code = "4780004000022" },
            new() { ProductId = products[15].Id, Code = "4780005000014" },
            new() { ProductId = products[16].Id, Code = "4780002000048" },
            new() { ProductId = products[17].Id, Code = "4780001000042" },
            new() { ProductId = products[18].Id, Code = "4780001000059" },
            new() { ProductId = products[19].Id, Code = "4780006000013" },
            new() { ProductId = products[20].Id, Code = "4780006000020" },
            new() { ProductId = products[21].Id, Code = "4780006000037" },
            new() { ProductId = products[22].Id, Code = "4780006000044" },
            new() { ProductId = products[23].Id, Code = "4780004000039" },
            new() { ProductId = products[24].Id, Code = "4780004000046" },
            new() { ProductId = products[25].Id, Code = "4780003000054" },
            new() { ProductId = products[26].Id, Code = "4780003000061" },
            new() { ProductId = products[27].Id, Code = "4780003000078" },
            new() { ProductId = products[28].Id, Code = "4780003000085" },
            new() { ProductId = products[29].Id, Code = "4780002000055" },
            new() { ProductId = products[30].Id, Code = "4780003000092" },
            new() { ProductId = products[31].Id, Code = "4780002000062" },
            new() { ProductId = products[32].Id, Code = "4780005000038" },
        };

        await context.Barcodes.AddRangeAsync(barcodes);

        var stocks = new List<Stock>
        {
            new() { ProductId = products[0].Id, WarehouseId = warehouse.Id, Quantity = 120, PurchasePrice = 8000 },
            new() { ProductId = products[1].Id, WarehouseId = warehouse.Id, Quantity = 100, PurchasePrice = 6000 },
            new() { ProductId = products[2].Id, WarehouseId = warehouse.Id, Quantity = 150, PurchasePrice = 4000 },
            new() { ProductId = products[3].Id, WarehouseId = warehouse.Id, Quantity = 80, PurchasePrice = 3000 },
            new() { ProductId = products[4].Id, WarehouseId = warehouse.Id, Quantity = 60, PurchasePrice = 3500 },
            new() { ProductId = products[5].Id, WarehouseId = warehouse.Id, Quantity = 50, PurchasePrice = 4000 },
            new() { ProductId = products[6].Id, WarehouseId = warehouse.Id, Quantity = 90, PurchasePrice = 8000 },
            new() { ProductId = products[7].Id, WarehouseId = warehouse.Id, Quantity = 70, PurchasePrice = 6000 },
            new() { ProductId = products[8].Id, WarehouseId = warehouse.Id, Quantity = 40, PurchasePrice = 7000 },
            new() { ProductId = products[9].Id, WarehouseId = warehouse.Id, Quantity = 200, PurchasePrice = 12000 },
            new() { ProductId = products[10].Id, WarehouseId = warehouse.Id, Quantity = 180, PurchasePrice = 10000 },
            new() { ProductId = products[11].Id, WarehouseId = warehouse.Id, Quantity = 100, PurchasePrice = 14000 },
            new() { ProductId = products[12].Id, WarehouseId = warehouse.Id, Quantity = 60, PurchasePrice = 18000 },
            new() { ProductId = products[13].Id, WarehouseId = warehouse.Id, Quantity = 45, PurchasePrice = 5000 },
            new() { ProductId = products[14].Id, WarehouseId = warehouse.Id, Quantity = 30, PurchasePrice = 15000 },
            new() { ProductId = products[15].Id, WarehouseId = warehouse.Id, Quantity = 3, PurchasePrice = 15000 },
            new() { ProductId = products[16].Id, WarehouseId = warehouse.Id, Quantity = 25, PurchasePrice = 6000 },
            new() { ProductId = products[17].Id, WarehouseId = warehouse.Id, Quantity = 40, PurchasePrice = 3500 },
            new() { ProductId = products[18].Id, WarehouseId = warehouse.Id, Quantity = 35, PurchasePrice = 4500 },
            new() { ProductId = products[19].Id, WarehouseId = warehouse.Id, Quantity = 4, PurchasePrice = 5500 },
            new() { ProductId = products[20].Id, WarehouseId = warehouse.Id, Quantity = 0, PurchasePrice = 8000 },
            new() { ProductId = products[21].Id, WarehouseId = warehouse.Id, Quantity = 15, PurchasePrice = 16000 },
            new() { ProductId = products[22].Id, WarehouseId = warehouse.Id, Quantity = 2, PurchasePrice = 7000 },
            new() { ProductId = products[23].Id, WarehouseId = warehouse.Id, Quantity = 20, PurchasePrice = 9000 },
            new() { ProductId = products[24].Id, WarehouseId = warehouse.Id, Quantity = 0, PurchasePrice = 4000 },
            new() { ProductId = products[25].Id, WarehouseId = warehouse.Id, Quantity = 55, PurchasePrice = 4500 },
            new() { ProductId = products[26].Id, WarehouseId = warehouse.Id, Quantity = 3, PurchasePrice = 3000 },
            new() { ProductId = products[27].Id, WarehouseId = warehouse.Id, Quantity = 1, PurchasePrice = 5000 },
            new() { ProductId = products[28].Id, WarehouseId = warehouse.Id, Quantity = 0, PurchasePrice = 4000 },
            new() { ProductId = products[29].Id, WarehouseId = warehouse.Id, Quantity = 65, PurchasePrice = 5500 },
            new() { ProductId = products[30].Id, WarehouseId = warehouse.Id, Quantity = 30, PurchasePrice = 9000 },
            new() { ProductId = products[31].Id, WarehouseId = warehouse.Id, Quantity = 4, PurchasePrice = 7000 },
            new() { ProductId = products[32].Id, WarehouseId = warehouse.Id, Quantity = 20, PurchasePrice = 20000 },
        };

        foreach (var s in stocks) s.BranchId = branch1.Id;
        await context.Stocks.AddRangeAsync(stocks);

        var branch2Stocks = new List<Stock>
        {
            new() { BranchId = branch2.Id, ProductId = products[0].Id, WarehouseId = warehouse2.Id, Quantity = 50, PurchasePrice = 8000 },
            new() { BranchId = branch2.Id, ProductId = products[3].Id, WarehouseId = warehouse2.Id, Quantity = 40, PurchasePrice = 3000 },
            new() { BranchId = branch2.Id, ProductId = products[9].Id, WarehouseId = warehouse2.Id, Quantity = 30, PurchasePrice = 12000 },
        };
        await context.Stocks.AddRangeAsync(branch2Stocks);

        var customer1 = new Customer { FullName = "Alisher Karimov", Phone = "+998901234567", CardBarcode = "CB001", DiscountPct = 5 };
        var customer2 = new Customer { FullName = "Dilnoza Rahimova", Phone = "+998935557788", CardBarcode = "CB002", DiscountPct = 3 };
        var customer3 = new Customer { FullName = "Jasur Toshmatov", Phone = "+998977778899", CardBarcode = "CB003" };

        await context.Customers.AddRangeAsync(customer1, customer2, customer3);
        await context.SaveChangesAsync();

        var customer1Bonus = new Account { CustomerId = customer1.Id, Name = "Bonus", Type = AccountType.Bonus, Balance = 15000 };
        var customer2Bonus = new Account { CustomerId = customer2.Id, Name = "Bonus", Type = AccountType.Bonus, Balance = 8000 };
        await context.Accounts.AddRangeAsync(customer1Bonus, customer2Bonus);
        await context.SaveChangesAsync();

        var now = DateTime.UtcNow;

        var sale1 = new Sale
        {
            WarehouseId = warehouse.Id,
            UserId = admin.Id,
            CustomerId = customer1.Id,
            TotalAmount = 60000,
            PaidCash = 60000,
            PaidCard = 0,
            PaidBonus = 0,
            DebtAmount = 0,
            Status = SaleStatus.Completed,
            CreatedAt = now.AddDays(-7)
        };

        var sale2 = new Sale
        {
            WarehouseId = warehouse.Id,
            UserId = admin.Id,
            CustomerId = customer2.Id,
            TotalAmount = 58500,
            PaidCash = 0,
            PaidCard = 58500,
            PaidBonus = 0,
            DebtAmount = 0,
            Status = SaleStatus.Completed,
            CreatedAt = now.AddDays(-6)
        };

        var sale3 = new Sale
        {
            WarehouseId = warehouse.Id,
            UserId = admin.Id,
            TotalAmount = 91000,
            PaidCash = 91000,
            PaidCard = 0,
            PaidBonus = 0,
            DebtAmount = 0,
            Status = SaleStatus.Completed,
            CreatedAt = now.AddDays(-5)
        };

        var sale4 = new Sale
        {
            WarehouseId = warehouse.Id,
            UserId = admin.Id,
            CustomerId = customer1.Id,
            TotalAmount = 35000,
            PaidCash = 20000,
            PaidCard = 15000,
            PaidBonus = 0,
            DebtAmount = 0,
            Status = SaleStatus.Completed,
            CreatedAt = now.AddDays(-5)
        };

        var sale5 = new Sale
        {
            WarehouseId = warehouse.Id,
            UserId = admin.Id,
            TotalAmount = 70000,
            PaidCash = 70000,
            PaidCard = 0,
            PaidBonus = 0,
            DebtAmount = 0,
            Status = SaleStatus.Completed,
            CreatedAt = now.AddDays(-4)
        };

        var sale6 = new Sale
        {
            WarehouseId = warehouse.Id,
            UserId = admin.Id,
            CustomerId = customer3.Id,
            TotalAmount = 72500,
            PaidCash = 0,
            PaidCard = 72500,
            PaidBonus = 0,
            DebtAmount = 0,
            Status = SaleStatus.Completed,
            CreatedAt = now.AddDays(-3)
        };

        var sale7 = new Sale
        {
            WarehouseId = warehouse.Id,
            UserId = admin.Id,
            TotalAmount = 35000,
            PaidCash = 35000,
            PaidCard = 0,
            PaidBonus = 0,
            DebtAmount = 0,
            Status = SaleStatus.Completed,
            CreatedAt = now.AddDays(-2)
        };

        var sale8 = new Sale
        {
            WarehouseId = warehouse.Id,
            UserId = admin.Id,
            CustomerId = customer2.Id,
            TotalAmount = 76000,
            PaidCash = 50000,
            PaidCard = 26000,
            PaidBonus = 0,
            DebtAmount = 0,
            Status = SaleStatus.Completed,
            CreatedAt = now.AddDays(-2)
        };

        var sale9 = new Sale
        {
            WarehouseId = warehouse.Id,
            UserId = admin.Id,
            TotalAmount = 37500,
            PaidCash = 37500,
            PaidCard = 0,
            PaidBonus = 0,
            DebtAmount = 0,
            Status = SaleStatus.Completed,
            CreatedAt = now.AddDays(-1)
        };

        var sale10 = new Sale
        {
            WarehouseId = warehouse.Id,
            UserId = admin.Id,
            CustomerId = customer1.Id,
            TotalAmount = 77000,
            PaidCash = 0,
            PaidCard = 77000,
            PaidBonus = 0,
            DebtAmount = 0,
            Status = SaleStatus.Completed,
            CreatedAt = now
        };

        var sale11 = new Sale
        {
            WarehouseId = warehouse.Id,
            UserId = admin.Id,
            CustomerId = customer1.Id,
            TotalAmount = 50000,
            PaidCash = 50000,
            PaidCard = 0,
            PaidBonus = 0,
            DebtAmount = 0,
            Status = SaleStatus.Completed,
            CreatedAt = now.AddDays(-13)
        };

        var sale12 = new Sale
        {
            WarehouseId = warehouse.Id,
            UserId = admin.Id,
            TotalAmount = 38500,
            PaidCash = 0,
            PaidCard = 38500,
            PaidBonus = 0,
            DebtAmount = 0,
            Status = SaleStatus.Completed,
            CreatedAt = now.AddDays(-12)
        };

        var sale13 = new Sale
        {
            WarehouseId = warehouse.Id,
            UserId = admin.Id,
            CustomerId = customer3.Id,
            TotalAmount = 54000,
            PaidCash = 30000,
            PaidCard = 24000,
            PaidBonus = 0,
            DebtAmount = 0,
            Status = SaleStatus.Completed,
            CreatedAt = now.AddDays(-11)
        };

        var sale14 = new Sale
        {
            WarehouseId = warehouse.Id,
            UserId = admin.Id,
            TotalAmount = 105000,
            PaidCash = 105000,
            PaidCard = 0,
            PaidBonus = 0,
            DebtAmount = 0,
            Status = SaleStatus.Completed,
            CreatedAt = now.AddDays(-10)
        };

        var sale15 = new Sale
        {
            WarehouseId = warehouse.Id,
            UserId = admin.Id,
            CustomerId = customer2.Id,
            TotalAmount = 76000,
            PaidCash = 0,
            PaidCard = 76000,
            PaidBonus = 0,
            DebtAmount = 0,
            Status = SaleStatus.Completed,
            CreatedAt = now.AddDays(-9)
        };

        var sale16 = new Sale
        {
            WarehouseId = warehouse.Id,
            UserId = admin.Id,
            TotalAmount = 42000,
            PaidCash = 42000,
            PaidCard = 0,
            PaidBonus = 0,
            DebtAmount = 0,
            Status = SaleStatus.Completed,
            CreatedAt = now.AddDays(-8)
        };

        var sale17 = new Sale
        {
            WarehouseId = warehouse.Id,
            UserId = admin.Id,
            CustomerId = customer1.Id,
            TotalAmount = 77000,
            PaidCash = 40000,
            PaidCard = 37000,
            PaidBonus = 0,
            DebtAmount = 0,
            Status = SaleStatus.Completed,
            CreatedAt = now.AddDays(-6)
        };

        var sale18 = new Sale
        {
            WarehouseId = warehouse.Id,
            UserId = admin.Id,
            TotalAmount = 62000,
            PaidCash = 0,
            PaidCard = 62000,
            PaidBonus = 0,
            DebtAmount = 0,
            Status = SaleStatus.Completed,
            CreatedAt = now.AddDays(-4)
        };

        var sale19 = new Sale
        {
            WarehouseId = warehouse.Id,
            UserId = admin.Id,
            CustomerId = customer3.Id,
            TotalAmount = 68000,
            PaidCash = 68000,
            PaidCard = 0,
            PaidBonus = 0,
            DebtAmount = 0,
            Status = SaleStatus.Completed,
            CreatedAt = now.AddDays(-2)
        };

        var sale20 = new Sale
        {
            WarehouseId = warehouse.Id,
            UserId = admin.Id,
            CustomerId = customer2.Id,
            TotalAmount = 94000,
            PaidCash = 50000,
            PaidCard = 44000,
            PaidBonus = 0,
            DebtAmount = 0,
            Status = SaleStatus.Completed,
            CreatedAt = now.AddDays(-1)
        };

        var allSales = new[] { sale1, sale2, sale3, sale4, sale5, sale6, sale7, sale8, sale9, sale10,
            sale11, sale12, sale13, sale14, sale15, sale16, sale17, sale18, sale19, sale20 };
        foreach (var s in allSales) s.BranchId = branch1.Id;
        await context.Sales.AddRangeAsync(allSales);

        var saleB2 = new Sale
        {
            BranchId = branch2.Id,
            WarehouseId = warehouse2.Id,
            UserId = admin.Id,
            TotalAmount = 14000,
            PaidCash = 14000,
            Status = SaleStatus.Completed,
            CreatedAt = now.AddDays(-1)
        };
        await context.Sales.AddAsync(saleB2);
        await context.SaveChangesAsync();

        var saleItems = new List<SaleItem>
        {
            new() { SaleId = sale1.Id, ProductId = products[0].Id, StockId = stocks[0].Id, Quantity = 2, UnitPrice = 10000, PurchasePrice = 8000 },
            new() { SaleId = sale1.Id, ProductId = products[3].Id, StockId = stocks[3].Id, Quantity = 5, UnitPrice = 4000, PurchasePrice = 3000 },
            new() { SaleId = sale1.Id, ProductId = products[6].Id, StockId = stocks[6].Id, Quantity = 2, UnitPrice = 10000, PurchasePrice = 8000 },

            new() { SaleId = sale2.Id, ProductId = products[1].Id, StockId = stocks[1].Id, Quantity = 3, UnitPrice = 7500, PurchasePrice = 6000 },
            new() { SaleId = sale2.Id, ProductId = products[4].Id, StockId = stocks[4].Id, Quantity = 4, UnitPrice = 4500, PurchasePrice = 3500 },
            new() { SaleId = sale2.Id, ProductId = products[8].Id, StockId = stocks[8].Id, Quantity = 2, UnitPrice = 9000, PurchasePrice = 7000 },

            new() { SaleId = sale3.Id, ProductId = products[9].Id, StockId = stocks[9].Id, Quantity = 3, UnitPrice = 15000, PurchasePrice = 12000 },
            new() { SaleId = sale3.Id, ProductId = products[10].Id, StockId = stocks[10].Id, Quantity = 2, UnitPrice = 12000, PurchasePrice = 10000 },
            new() { SaleId = sale3.Id, ProductId = products[12].Id, StockId = stocks[12].Id, Quantity = 1, UnitPrice = 22000, PurchasePrice = 18000 },

            new() { SaleId = sale4.Id, ProductId = products[2].Id, StockId = stocks[2].Id, Quantity = 4, UnitPrice = 5000, PurchasePrice = 4000 },
            new() { SaleId = sale4.Id, ProductId = products[5].Id, StockId = stocks[5].Id, Quantity = 3, UnitPrice = 5000, PurchasePrice = 4000 },

            new() { SaleId = sale5.Id, ProductId = products[3].Id, StockId = stocks[3].Id, Quantity = 10, UnitPrice = 4000, PurchasePrice = 3000 },
            new() { SaleId = sale5.Id, ProductId = products[6].Id, StockId = stocks[6].Id, Quantity = 3, UnitPrice = 10000, PurchasePrice = 8000 },

            new() { SaleId = sale6.Id, ProductId = products[11].Id, StockId = stocks[11].Id, Quantity = 2, UnitPrice = 17000, PurchasePrice = 14000 },
            new() { SaleId = sale6.Id, ProductId = products[13].Id, StockId = stocks[13].Id, Quantity = 3, UnitPrice = 6500, PurchasePrice = 5000 },
            new() { SaleId = sale6.Id, ProductId = products[14].Id, StockId = stocks[14].Id, Quantity = 1, UnitPrice = 19000, PurchasePrice = 15000 },

            new() { SaleId = sale7.Id, ProductId = products[0].Id, StockId = stocks[0].Id, Quantity = 1, UnitPrice = 10000, PurchasePrice = 8000 },
            new() { SaleId = sale7.Id, ProductId = products[2].Id, StockId = stocks[2].Id, Quantity = 2, UnitPrice = 5000, PurchasePrice = 4000 },
            new() { SaleId = sale7.Id, ProductId = products[7].Id, StockId = stocks[7].Id, Quantity = 2, UnitPrice = 7500, PurchasePrice = 6000 },

            new() { SaleId = sale8.Id, ProductId = products[9].Id, StockId = stocks[9].Id, Quantity = 2, UnitPrice = 15000, PurchasePrice = 12000 },
            new() { SaleId = sale8.Id, ProductId = products[10].Id, StockId = stocks[10].Id, Quantity = 1, UnitPrice = 12000, PurchasePrice = 10000 },
            new() { SaleId = sale8.Id, ProductId = products[3].Id, StockId = stocks[3].Id, Quantity = 3, UnitPrice = 4000, PurchasePrice = 3000 },
            new() { SaleId = sale8.Id, ProductId = products[12].Id, StockId = stocks[12].Id, Quantity = 1, UnitPrice = 22000, PurchasePrice = 18000 },

            new() { SaleId = sale9.Id, ProductId = products[1].Id, StockId = stocks[1].Id, Quantity = 2, UnitPrice = 7500, PurchasePrice = 6000 },
            new() { SaleId = sale9.Id, ProductId = products[4].Id, StockId = stocks[4].Id, Quantity = 5, UnitPrice = 4500, PurchasePrice = 3500 },

            new() { SaleId = sale10.Id, ProductId = products[6].Id, StockId = stocks[6].Id, Quantity = 4, UnitPrice = 10000, PurchasePrice = 8000 },
            new() { SaleId = sale10.Id, ProductId = products[8].Id, StockId = stocks[8].Id, Quantity = 3, UnitPrice = 9000, PurchasePrice = 7000 },
            new() { SaleId = sale10.Id, ProductId = products[5].Id, StockId = stocks[5].Id, Quantity = 2, UnitPrice = 5000, PurchasePrice = 4000 },

            new() { SaleId = sale11.Id, ProductId = products[0].Id, StockId = stocks[0].Id, Quantity = 3, UnitPrice = 10000, PurchasePrice = 8000 },
            new() { SaleId = sale11.Id, ProductId = products[17].Id, StockId = stocks[17].Id, Quantity = 4, UnitPrice = 5000, PurchasePrice = 3500 },

            new() { SaleId = sale12.Id, ProductId = products[16].Id, StockId = stocks[16].Id, Quantity = 2, UnitPrice = 8000, PurchasePrice = 6000 },
            new() { SaleId = sale12.Id, ProductId = products[29].Id, StockId = stocks[29].Id, Quantity = 3, UnitPrice = 7500, PurchasePrice = 5500 },

            new() { SaleId = sale13.Id, ProductId = products[25].Id, StockId = stocks[25].Id, Quantity = 5, UnitPrice = 6000, PurchasePrice = 4500 },
            new() { SaleId = sale13.Id, ProductId = products[30].Id, StockId = stocks[30].Id, Quantity = 2, UnitPrice = 12000, PurchasePrice = 9000 },

            new() { SaleId = sale14.Id, ProductId = products[9].Id, StockId = stocks[9].Id, Quantity = 2, UnitPrice = 15000, PurchasePrice = 12000 },
            new() { SaleId = sale14.Id, ProductId = products[32].Id, StockId = stocks[32].Id, Quantity = 3, UnitPrice = 25000, PurchasePrice = 20000 },

            new() { SaleId = sale15.Id, ProductId = products[18].Id, StockId = stocks[18].Id, Quantity = 6, UnitPrice = 6000, PurchasePrice = 4500 },
            new() { SaleId = sale15.Id, ProductId = products[21].Id, StockId = stocks[21].Id, Quantity = 2, UnitPrice = 20000, PurchasePrice = 16000 },

            new() { SaleId = sale16.Id, ProductId = products[15].Id, StockId = stocks[15].Id, Quantity = 1, UnitPrice = 18000, PurchasePrice = 15000 },
            new() { SaleId = sale16.Id, ProductId = products[23].Id, StockId = stocks[23].Id, Quantity = 2, UnitPrice = 12000, PurchasePrice = 9000 },

            new() { SaleId = sale17.Id, ProductId = products[0].Id, StockId = stocks[0].Id, Quantity = 5, UnitPrice = 10000, PurchasePrice = 8000 },
            new() { SaleId = sale17.Id, ProductId = products[29].Id, StockId = stocks[29].Id, Quantity = 2, UnitPrice = 7500, PurchasePrice = 5500 },
            new() { SaleId = sale17.Id, ProductId = products[30].Id, StockId = stocks[30].Id, Quantity = 1, UnitPrice = 12000, PurchasePrice = 9000 },

            new() { SaleId = sale18.Id, ProductId = products[25].Id, StockId = stocks[25].Id, Quantity = 3, UnitPrice = 6000, PurchasePrice = 4500 },
            new() { SaleId = sale18.Id, ProductId = products[12].Id, StockId = stocks[12].Id, Quantity = 2, UnitPrice = 22000, PurchasePrice = 18000 },

            new() { SaleId = sale19.Id, ProductId = products[17].Id, StockId = stocks[17].Id, Quantity = 10, UnitPrice = 5000, PurchasePrice = 3500 },
            new() { SaleId = sale19.Id, ProductId = products[31].Id, StockId = stocks[31].Id, Quantity = 2, UnitPrice = 9000, PurchasePrice = 7000 },

            new() { SaleId = sale20.Id, ProductId = products[32].Id, StockId = stocks[32].Id, Quantity = 2, UnitPrice = 25000, PurchasePrice = 20000 },
            new() { SaleId = sale20.Id, ProductId = products[19].Id, StockId = stocks[19].Id, Quantity = 2, UnitPrice = 7000, PurchasePrice = 5500 },
            new() { SaleId = sale20.Id, ProductId = products[6].Id, StockId = stocks[6].Id, Quantity = 3, UnitPrice = 10000, PurchasePrice = 8000 },
        };

        await context.SaleItems.AddRangeAsync(saleItems);

        stocks[0].Quantity -= 3;
        stocks[1].Quantity -= 5;
        stocks[2].Quantity -= 6;
        stocks[3].Quantity -= 18;
        stocks[4].Quantity -= 9;
        stocks[5].Quantity -= 5;
        stocks[6].Quantity -= 9;
        stocks[7].Quantity -= 2;
        stocks[8].Quantity -= 5;
        stocks[9].Quantity -= 5;
        stocks[10].Quantity -= 3;
        stocks[11].Quantity -= 2;
        stocks[12].Quantity -= 2;
        stocks[13].Quantity -= 3;
        stocks[14].Quantity -= 1;

        stocks[0].Quantity -= 8;
        stocks[6].Quantity -= 3;
        stocks[9].Quantity -= 2;
        stocks[12].Quantity -= 2;
        stocks[15].Quantity -= 1;
        stocks[16].Quantity -= 2;
        stocks[17].Quantity -= 14;
        stocks[18].Quantity -= 6;
        stocks[19].Quantity -= 2;
        stocks[21].Quantity -= 2;
        stocks[23].Quantity -= 2;
        stocks[25].Quantity -= 8;
        stocks[29].Quantity -= 5;
        stocks[30].Quantity -= 3;
        stocks[31].Quantity -= 2;
        stocks[32].Quantity -= 5;

        shopCashAccount.Balance = 748500;
        shopCardAccount.Balance = 530500;
        branch2CashAccount.Balance = 14000;

        var transactions = new List<Transaction>
        {
            new() { ToAccountId = shopCashAccount.Id, Amount = 60000, OperationType = OperationType.Sale, SaleId = sale1.Id, UserId = admin.Id, CreatedAt = sale1.CreatedAt },
            new() { ToAccountId = shopCardAccount.Id, Amount = 58500, OperationType = OperationType.Sale, SaleId = sale2.Id, UserId = admin.Id, CreatedAt = sale2.CreatedAt },
            new() { ToAccountId = shopCashAccount.Id, Amount = 91000, OperationType = OperationType.Sale, SaleId = sale3.Id, UserId = admin.Id, CreatedAt = sale3.CreatedAt },
            new() { ToAccountId = shopCashAccount.Id, Amount = 20000, OperationType = OperationType.Sale, SaleId = sale4.Id, UserId = admin.Id, CreatedAt = sale4.CreatedAt },
            new() { ToAccountId = shopCardAccount.Id, Amount = 15000, OperationType = OperationType.Sale, SaleId = sale4.Id, UserId = admin.Id, CreatedAt = sale4.CreatedAt },
            new() { ToAccountId = shopCashAccount.Id, Amount = 70000, OperationType = OperationType.Sale, SaleId = sale5.Id, UserId = admin.Id, CreatedAt = sale5.CreatedAt },
            new() { ToAccountId = shopCardAccount.Id, Amount = 72500, OperationType = OperationType.Sale, SaleId = sale6.Id, UserId = admin.Id, CreatedAt = sale6.CreatedAt },
            new() { ToAccountId = shopCashAccount.Id, Amount = 35000, OperationType = OperationType.Sale, SaleId = sale7.Id, UserId = admin.Id, CreatedAt = sale7.CreatedAt },
            new() { ToAccountId = shopCashAccount.Id, Amount = 50000, OperationType = OperationType.Sale, SaleId = sale8.Id, UserId = admin.Id, CreatedAt = sale8.CreatedAt },
            new() { ToAccountId = shopCardAccount.Id, Amount = 26000, OperationType = OperationType.Sale, SaleId = sale8.Id, UserId = admin.Id, CreatedAt = sale8.CreatedAt },
            new() { ToAccountId = shopCashAccount.Id, Amount = 37500, OperationType = OperationType.Sale, SaleId = sale9.Id, UserId = admin.Id, CreatedAt = sale9.CreatedAt },
            new() { ToAccountId = shopCardAccount.Id, Amount = 77000, OperationType = OperationType.Sale, SaleId = sale10.Id, UserId = admin.Id, CreatedAt = sale10.CreatedAt },
            new() { ToAccountId = shopCashAccount.Id, Amount = 50000, OperationType = OperationType.Sale, SaleId = sale11.Id, UserId = admin.Id, CreatedAt = sale11.CreatedAt },
            new() { ToAccountId = shopCardAccount.Id, Amount = 38500, OperationType = OperationType.Sale, SaleId = sale12.Id, UserId = admin.Id, CreatedAt = sale12.CreatedAt },
            new() { ToAccountId = shopCashAccount.Id, Amount = 30000, OperationType = OperationType.Sale, SaleId = sale13.Id, UserId = admin.Id, CreatedAt = sale13.CreatedAt },
            new() { ToAccountId = shopCardAccount.Id, Amount = 24000, OperationType = OperationType.Sale, SaleId = sale13.Id, UserId = admin.Id, CreatedAt = sale13.CreatedAt },
            new() { ToAccountId = shopCashAccount.Id, Amount = 105000, OperationType = OperationType.Sale, SaleId = sale14.Id, UserId = admin.Id, CreatedAt = sale14.CreatedAt },
            new() { ToAccountId = shopCardAccount.Id, Amount = 76000, OperationType = OperationType.Sale, SaleId = sale15.Id, UserId = admin.Id, CreatedAt = sale15.CreatedAt },
            new() { ToAccountId = shopCashAccount.Id, Amount = 42000, OperationType = OperationType.Sale, SaleId = sale16.Id, UserId = admin.Id, CreatedAt = sale16.CreatedAt },
            new() { ToAccountId = shopCashAccount.Id, Amount = 40000, OperationType = OperationType.Sale, SaleId = sale17.Id, UserId = admin.Id, CreatedAt = sale17.CreatedAt },
            new() { ToAccountId = shopCardAccount.Id, Amount = 37000, OperationType = OperationType.Sale, SaleId = sale17.Id, UserId = admin.Id, CreatedAt = sale17.CreatedAt },
            new() { ToAccountId = shopCardAccount.Id, Amount = 62000, OperationType = OperationType.Sale, SaleId = sale18.Id, UserId = admin.Id, CreatedAt = sale18.CreatedAt },
            new() { ToAccountId = shopCashAccount.Id, Amount = 68000, OperationType = OperationType.Sale, SaleId = sale19.Id, UserId = admin.Id, CreatedAt = sale19.CreatedAt },
            new() { ToAccountId = shopCashAccount.Id, Amount = 50000, OperationType = OperationType.Sale, SaleId = sale20.Id, UserId = admin.Id, CreatedAt = sale20.CreatedAt },
            new() { ToAccountId = shopCardAccount.Id, Amount = 44000, OperationType = OperationType.Sale, SaleId = sale20.Id, UserId = admin.Id, CreatedAt = sale20.CreatedAt },
            new() { ToAccountId = branch2CashAccount.Id, Amount = 14000, OperationType = OperationType.Sale, SaleId = saleB2.Id, UserId = admin.Id, CreatedAt = saleB2.CreatedAt },
            new() { ToAccountId = customer1Bonus.Id, Amount = 15000, OperationType = OperationType.Cashback, UserId = admin.Id, CreatedAt = now.AddDays(-14) },
            new() { ToAccountId = customer2Bonus.Id, Amount = 8000, OperationType = OperationType.Cashback, UserId = admin.Id, CreatedAt = now.AddDays(-14) },
        };

        await context.Transactions.AddRangeAsync(transactions);

        await context.SaveChangesAsync();
    }
}
