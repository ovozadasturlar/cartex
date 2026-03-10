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

        var dona = defaultUnits[0];
        var kg = defaultUnits[1];
        var litr = defaultUnits[2];

        var catFood = new Category { Name = "Oziq-ovqat" };
        var catBeverages = new Category { Name = "Ichimliklar" };
        var catDairy = new Category { Name = "Sut mahsulotlari" };
        var catBakery = new Category { Name = "Non mahsulotlari" };
        var catHousehold = new Category { Name = "Uy-ro'zg'or" };
        var catPersonalCare = new Category { Name = "Shaxsiy gigiyena" };

        await context.Categories.AddRangeAsync(catFood, catBeverages, catDairy, catBakery, catHousehold, catPersonalCare);
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
        };

        await context.Products.AddRangeAsync(products);
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
        };

        await context.Barcodes.AddRangeAsync(barcodes);

        var stocks = new List<Stock>
        {
            new() { ProductId = products[0].Id, WarehouseId = warehouse.Id, Quantity = 120, PurchasePrice = 8000, SellingPrice = 10000 },
            new() { ProductId = products[1].Id, WarehouseId = warehouse.Id, Quantity = 100, PurchasePrice = 6000, SellingPrice = 7500 },
            new() { ProductId = products[2].Id, WarehouseId = warehouse.Id, Quantity = 150, PurchasePrice = 4000, SellingPrice = 5000 },
            new() { ProductId = products[3].Id, WarehouseId = warehouse.Id, Quantity = 80, PurchasePrice = 3000, SellingPrice = 4000 },
            new() { ProductId = products[4].Id, WarehouseId = warehouse.Id, Quantity = 60, PurchasePrice = 3500, SellingPrice = 4500 },
            new() { ProductId = products[5].Id, WarehouseId = warehouse.Id, Quantity = 50, PurchasePrice = 4000, SellingPrice = 5000 },
            new() { ProductId = products[6].Id, WarehouseId = warehouse.Id, Quantity = 90, PurchasePrice = 8000, SellingPrice = 10000 },
            new() { ProductId = products[7].Id, WarehouseId = warehouse.Id, Quantity = 70, PurchasePrice = 6000, SellingPrice = 7500 },
            new() { ProductId = products[8].Id, WarehouseId = warehouse.Id, Quantity = 40, PurchasePrice = 7000, SellingPrice = 9000 },
            new() { ProductId = products[9].Id, WarehouseId = warehouse.Id, Quantity = 200, PurchasePrice = 12000, SellingPrice = 15000 },
            new() { ProductId = products[10].Id, WarehouseId = warehouse.Id, Quantity = 180, PurchasePrice = 10000, SellingPrice = 12000 },
            new() { ProductId = products[11].Id, WarehouseId = warehouse.Id, Quantity = 100, PurchasePrice = 14000, SellingPrice = 17000 },
            new() { ProductId = products[12].Id, WarehouseId = warehouse.Id, Quantity = 60, PurchasePrice = 18000, SellingPrice = 22000 },
            new() { ProductId = products[13].Id, WarehouseId = warehouse.Id, Quantity = 45, PurchasePrice = 5000, SellingPrice = 6500 },
            new() { ProductId = products[14].Id, WarehouseId = warehouse.Id, Quantity = 30, PurchasePrice = 15000, SellingPrice = 19000 },
        };

        await context.Stocks.AddRangeAsync(stocks);

        var customer1 = new Customer { FullName = "Alisher Karimov", Phone = "+998901234567", CardBarcode = "CB001", DiscountPct = 5, CashbackBalance = 15000 };
        var customer2 = new Customer { FullName = "Dilnoza Rahimova", Phone = "+998935557788", CardBarcode = "CB002", DiscountPct = 3, CashbackBalance = 8000 };
        var customer3 = new Customer { FullName = "Jasur Toshmatov", Phone = "+998977778899", CardBarcode = "CB003", DiscountPct = 0, CashbackBalance = 0 };

        await context.Customers.AddRangeAsync(customer1, customer2, customer3);
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

        await context.Sales.AddRangeAsync(sale1, sale2, sale3, sale4, sale5, sale6, sale7, sale8, sale9, sale10);
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

        shopCashAccount.Balance = 363500;
        shopCardAccount.Balance = 249000;

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
        };

        await context.Transactions.AddRangeAsync(transactions);

        await context.SaveChangesAsync();
    }
}
