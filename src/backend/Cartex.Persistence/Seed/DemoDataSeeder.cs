using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Persistence.Seed;

public sealed record DemoCatalog(
    Branch Branch1, Warehouse Wh1, Warehouse Wh2,
    User Admin, User Seller, Account Cash, Account Card,
    List<ProductVariant> Variants, decimal[] SellingPrices, List<Stock> Stocks,
    Supplier[] Suppliers, List<Customer> Customers,
    DateTime OpeningDate, DateTime Today0, DateTime Now);

public static class DemoDataSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        var catalog = await SeedCatalogAsync(context);
        if (catalog is null)
            return;
        await SeedTransactionsAsync(context, catalog);
    }

    public static async Task<DemoCatalog?> SeedCatalogAsync(ApplicationDbContext context)
    {
        if (await context.BusinessSettings.AnyAsync(s => s.Key == "demo_seeded" && s.Value == "true"))
            return null;
        if (await context.Products.AnyAsync())
            return null;

        var now = DateTime.UtcNow;
        var today0 = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var openingDate = today0.AddDays(-30).AddHours(8);

        var branch1 = await context.Branches.OrderBy(b => b.Id).FirstAsync();
        var wh1 = await context.Warehouses.FirstAsync(w => w.BranchId == branch1.Id);
        var admin = await context.Users.FirstAsync(u => u.Username == "admin");
        var seller = await context.Users.FirstAsync(u => u.Username == "seller");
        var cash = await context.Accounts.FirstAsync(a => a.BranchId == branch1.Id && a.Type == AccountType.Cash);
        var card = await context.Accounts.FirstAsync(a => a.BranchId == branch1.Id && a.Type == AccountType.Card);

        var branch2 = new Branch { BusinessId = branch1.BusinessId, Name = "Filial 2", Address = "Samarqand" };
        await context.Branches.AddAsync(branch2);
        await context.SaveChangesAsync();
        var wh2 = new Warehouse { BranchId = branch2.Id, Name = "Filial 2 ombori" };
        await context.Warehouses.AddAsync(wh2);
        await context.SaveChangesAsync();

        var typeRegular = await context.ProductTypes.FirstAsync(t => t.Name == "Oddiy mahsulot");
        var typeExpiring = await context.ProductTypes.FirstAsync(t => t.Name == "Muddatli mahsulot");
        var dona = await context.Units.FirstAsync(u => u.ShortName == "dona");
        var metr = await context.Units.FirstAsync(u => u.ShortName == "m");

        var catMixers = new Category { Name = "Smesitellar" };
        var catPipes = new Category { Name = "Trubalar va fitinglar" };
        var catValves = new Category { Name = "Kranlar va ventillar" };
        var catSewage = new Category { Name = "Kanalizatsiya" };
        var catFixtures = new Category { Name = "Santexnika jihozlari" };
        var catSealants = new Category { Name = "Germetik va yelimlar" };
        var catHeating = new Category { Name = "Isitish" };
        await context.Categories.AddRangeAsync(catMixers, catPipes, catValves, catSewage, catFixtures, catSealants, catHeating);
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
            new() { BranchId = branch1.Id, VariantId = variants[0].Id, WarehouseId = wh1.Id, Quantity = 15, PurchasePrice = 265000, CreatedAt = openingDate },
            new() { BranchId = branch1.Id, VariantId = variants[1].Id, WarehouseId = wh1.Id, Quantity = 6, PurchasePrice = 360000, CreatedAt = openingDate },
            new() { BranchId = branch1.Id, VariantId = variants[2].Id, WarehouseId = wh1.Id, Quantity = 10, PurchasePrice = 205000, CreatedAt = openingDate },
            new() { BranchId = branch1.Id, VariantId = variants[3].Id, WarehouseId = wh1.Id, Quantity = 200, PurchasePrice = 6200, CreatedAt = openingDate },
            new() { BranchId = branch1.Id, VariantId = variants[4].Id, WarehouseId = wh1.Id, Quantity = 160, PurchasePrice = 9100, CreatedAt = openingDate },
            new() { BranchId = branch1.Id, VariantId = variants[5].Id, WarehouseId = wh1.Id, Quantity = 120, PurchasePrice = 14300, CreatedAt = openingDate },
            new() { BranchId = branch1.Id, VariantId = variants[6].Id, WarehouseId = wh1.Id, Quantity = 150, PurchasePrice = 900, CreatedAt = openingDate },
            new() { BranchId = branch1.Id, VariantId = variants[7].Id, WarehouseId = wh1.Id, Quantity = 140, PurchasePrice = 1100, CreatedAt = openingDate },
            new() { BranchId = branch1.Id, VariantId = variants[8].Id, WarehouseId = wh1.Id, Quantity = 100, PurchasePrice = 2200, CreatedAt = openingDate },
            new() { BranchId = branch1.Id, VariantId = variants[9].Id, WarehouseId = wh1.Id, Quantity = 80, PurchasePrice = 7800, CreatedAt = openingDate },
            new() { BranchId = branch1.Id, VariantId = variants[10].Id, WarehouseId = wh1.Id, Quantity = 35, PurchasePrice = 26000, CreatedAt = openingDate },
            new() { BranchId = branch1.Id, VariantId = variants[11].Id, WarehouseId = wh1.Id, Quantity = 30, PurchasePrice = 36000, CreatedAt = openingDate },
            new() { BranchId = branch1.Id, VariantId = variants[12].Id, WarehouseId = wh1.Id, Quantity = 25, PurchasePrice = 31000, CreatedAt = openingDate },
            new() { BranchId = branch1.Id, VariantId = variants[13].Id, WarehouseId = wh1.Id, Quantity = 30, PurchasePrice = 23000, CreatedAt = openingDate },
            new() { BranchId = branch1.Id, VariantId = variants[14].Id, WarehouseId = wh1.Id, Quantity = 20, PurchasePrice = 33000, CreatedAt = openingDate },
            new() { BranchId = branch1.Id, VariantId = variants[15].Id, WarehouseId = wh1.Id, Quantity = 60, PurchasePrice = 18500, CreatedAt = openingDate },
            new() { BranchId = branch1.Id, VariantId = variants[16].Id, WarehouseId = wh1.Id, Quantity = 30, PurchasePrice = 44000, CreatedAt = openingDate },
            new() { BranchId = branch1.Id, VariantId = variants[17].Id, WarehouseId = wh1.Id, Quantity = 90, PurchasePrice = 4200, CreatedAt = openingDate },
            new() { BranchId = branch1.Id, VariantId = variants[18].Id, WarehouseId = wh1.Id, Quantity = 20, PurchasePrice = 64000, CreatedAt = openingDate },
            new() { BranchId = branch1.Id, VariantId = variants[19].Id, WarehouseId = wh1.Id, Quantity = 5, PurchasePrice = 290000, CreatedAt = openingDate },
            new() { BranchId = branch1.Id, VariantId = variants[20].Id, WarehouseId = wh1.Id, Quantity = 30, PurchasePrice = 37000, CreatedAt = openingDate },
            new() { BranchId = branch1.Id, VariantId = variants[21].Id, WarehouseId = wh1.Id, Quantity = 35, PurchasePrice = 27000, CreatedAt = openingDate },
            new() { BranchId = branch1.Id, VariantId = variants[22].Id, WarehouseId = wh1.Id, Quantity = 40, PurchasePrice = 11500, CreatedAt = openingDate },
            new() { BranchId = branch1.Id, VariantId = variants[23].Id, WarehouseId = wh1.Id, Quantity = 20, PurchasePrice = 58000, CreatedAt = openingDate },
            new() { BranchId = branch1.Id, VariantId = variants[24].Id, WarehouseId = wh1.Id, Quantity = 150, PurchasePrice = 2400, CreatedAt = openingDate },
            new() { BranchId = branch1.Id, VariantId = variants[25].Id, WarehouseId = wh1.Id, Quantity = 60, PurchasePrice = 5100, CreatedAt = openingDate },
            new() { BranchId = branch1.Id, VariantId = variants[26].Id, WarehouseId = wh1.Id, Quantity = 40, PurchasePrice = 28000, CreatedAt = openingDate },
            new() { BranchId = branch1.Id, VariantId = variants[27].Id, WarehouseId = wh1.Id, Quantity = 25, PurchasePrice = 37500, CreatedAt = openingDate },
            new() { BranchId = branch1.Id, VariantId = variants[28].Id, WarehouseId = wh1.Id, Quantity = 15, PurchasePrice = 82000, CreatedAt = openingDate },
            new() { BranchId = branch1.Id, VariantId = variants[29].Id, WarehouseId = wh1.Id, Quantity = 20, PurchasePrice = 82000, CreatedAt = openingDate },
            new() { BranchId = branch1.Id, VariantId = variants[30].Id, WarehouseId = wh1.Id, Quantity = 5, PurchasePrice = 680000, CreatedAt = openingDate },
            new() { BranchId = branch1.Id, VariantId = variants[31].Id, WarehouseId = wh1.Id, Quantity = 15, PurchasePrice = 128000, CreatedAt = openingDate },
            new() { BranchId = branch1.Id, VariantId = variants[32].Id, WarehouseId = wh1.Id, Quantity = 90, PurchasePrice = 5600, CreatedAt = openingDate },
        };
        await context.Stocks.AddRangeAsync(stocks);

        var branch2Stocks = new List<Stock>
        {
            new() { BranchId = branch2.Id, VariantId = variants[0].Id, WarehouseId = wh2.Id, Quantity = 4, PurchasePrice = 265000, CreatedAt = openingDate },
            new() { BranchId = branch2.Id, VariantId = variants[3].Id, WarehouseId = wh2.Id, Quantity = 80, PurchasePrice = 6200, CreatedAt = openingDate },
            new() { BranchId = branch2.Id, VariantId = variants[9].Id, WarehouseId = wh2.Id, Quantity = 30, PurchasePrice = 7800, CreatedAt = openingDate },
        };
        await context.Stocks.AddRangeAsync(branch2Stocks);

        var suppliers = new[]
        {
            new Supplier { Name = "Santex Optom Savdo", Phone = "+998711110011", CreatedAt = openingDate },
            new Supplier { Name = "AquaTrade Toshkent", Phone = "+998711110022", CreatedAt = openingDate },
            new Supplier { Name = "Polimer Plast Zavod", Phone = "+998711110033", CreatedAt = openingDate },
            new Supplier { Name = "Termo Group Distribution", Phone = "+998711110044", CreatedAt = openingDate },
        };
        await context.Suppliers.AddRangeAsync(suppliers);

        var custInfo = new (string Full, string Phone, string Card, decimal Disc, decimal Limit)[]
        {
            ("Alisher Karimov", "+998901112201", "DC1001", 5, 2_000_000),
            ("Dilnoza Rahimova", "+998901112202", "DC1002", 3, 0),
            ("Jasur Toshmatov", "+998901112203", "DC1003", 0, 1_500_000),
            ("Nodira Yusupova", "+998901112204", "DC1004", 2, 0),
            ("Sardor Aliyev", "+998901112205", "DC1005", 0, 3_000_000),
            ("Kamola Saidova", "+998901112206", "DC1006", 4, 0),
            ("Bekzod Ergashev", "+998901112207", "DC1007", 0, 1_000_000),
            ("Malika Islomova", "+998901112208", "DC1008", 2, 0),
            ("Otabek Norov", "+998901112209", "DC1009", 0, 0),
            ("Zulfiya Qodirova", "+998901112210", "DC1010", 3, 2_500_000),
        };
        var customers = custInfo.Select(c => new Customer
        {
            Party = new Party
            {
                BusinessId = branch1.BusinessId,
                FullName = c.Full,
                Phone = c.Phone,
                CreatedAt = openingDate
            },
            FullName = c.Full,
            Phone = c.Phone,
            CardBarcode = c.Card,
            DiscountPct = c.Disc,
            CreditLimit = c.Limit,
            CreatedAt = openingDate
        }).ToList();
        await context.Customers.AddRangeAsync(customers);
        await context.SaveChangesAsync();

        return new DemoCatalog(branch1, wh1, wh2, admin, seller, cash, card, variants, sellingPrices, stocks, suppliers, customers, openingDate, today0, now);
    }

    public static async Task SeedTransactionsAsync(ApplicationDbContext context, DemoCatalog cat)
    {
        if (await context.Sales.AnyAsync())
            return;

        var rnd = new Random(0xCA27EC);
        var openingDate = cat.OpeningDate;
        var today0 = cat.Today0;
        DateTime Day(int offset) => today0.AddDays(offset);

        var branch1 = cat.Branch1;
        var wh1 = cat.Wh1;
        var wh2 = cat.Wh2;
        var admin = cat.Admin;
        var seller = cat.Seller;
        var cash = cat.Cash;
        var card = cat.Card;
        var variants = cat.Variants;
        var sellingPrices = cat.SellingPrices;
        var stocks = cat.Stocks;
        var suppliers = cat.Suppliers;
        var customers = cat.Customers;

        var prices = variants.Select((v, i) => (v.Id, sellingPrices[i])).ToDictionary(x => x.Id, x => x.Item2);
        var purchase = stocks.GroupBy(s => s.VariantId).ToDictionary(g => g.Key, g => g.First().PurchasePrice);
        var allVids = prices.Keys.OrderBy(x => x).ToList();

        decimal Buy(long vid) => purchase.TryGetValue(vid, out var p) && p > 0 ? p : Math.Round(prices[vid] * 0.72m / 100m) * 100m;
        decimal R(decimal x, decimal step) => Math.Round(x / step, MidpointRounding.AwayFromZero) * step;

        var lots = new Dictionary<long, List<Stock>>();
        void AddLot(Stock s)
        {
            if (!lots.TryGetValue(s.VariantId, out var list)) lots[s.VariantId] = list = [];
            list.Add(s);
        }

        foreach (var s in stocks)
            if (s.Quantity > 0) AddLot(s);

        var supplies = new List<Supply>();
        decimal totalSupply = 0m;
        void AddSupply(Supplier sup, DateTime when, IEnumerable<long> vids, decimal qty)
        {
            var supply = new Supply
            {
                BranchId = branch1.Id, Supplier = sup, WarehouseId = wh1.Id, UserId = admin.Id,
                SupplyDate = DateOnly.FromDateTime(when), CreatedAt = when
            };
            decimal total = 0m;
            foreach (var vid in vids)
            {
                var pp = Buy(vid);
                supply.Items.Add(new SupplyItem { VariantId = vid, Quantity = qty, PurchasePrice = pp });
                var lot = new Stock { BranchId = branch1.Id, VariantId = vid, WarehouseId = wh1.Id, Supply = supply, Quantity = qty, PurchasePrice = pp, CreatedAt = when };
                AddLot(lot);
                context.Stocks.Add(lot);
                total += qty * pp;
            }
            supply.TotalAmount = total;
            totalSupply += total;
            supplies.Add(supply);
            context.Supplies.Add(supply);
        }

        var groups = new List<long>[] { [], [], [], [] };
        for (var i = 0; i < allVids.Count; i++) groups[i % 4].Add(allVids[i]);

        AddSupply(suppliers[0], Day(-29).AddHours(10), groups[0], 60);
        AddSupply(suppliers[1], Day(-28).AddHours(10), groups[1], 60);
        AddSupply(suppliers[2], Day(-27).AddHours(10), groups[2], 60);
        AddSupply(suppliers[3], Day(-26).AddHours(10), groups[3], 60);
        AddSupply(suppliers[0], Day(-15).AddHours(10), groups[0].Take(4), 40);
        AddSupply(suppliers[3], Day(-7).AddHours(10), groups[3].Take(4), 40);

        var bonusAcc = new Dictionary<Customer, Account>();
        var debtAcc = new Dictionary<Customer, Account>();
        foreach (var c in customers)
        {
            var b = new Account { Customer = c, Name = "Bonus", Type = AccountType.Bonus, CreatedAt = openingDate };
            var d = new Account { Customer = c, Name = "Qarz", Type = AccountType.Debt, CreatedAt = openingDate };
            await context.Accounts.AddRangeAsync(b, d);
            bonusAcc[c] = b;
            debtAcc[c] = d;
        }

        Transaction Post(OperationType type, decimal amount, Account? from, Account? to, long userId, DateTime date, Sale? sale = null, Supply? supply = null, Shift? shift = null)
        {
            if (from is not null) from.Balance -= amount;
            if (to is not null) to.Balance += amount;
            var t = new Transaction
            {
                FromAccount = from, ToAccount = to, Amount = amount, OperationType = type,
                BranchId = from?.BranchId ?? to?.BranchId ?? branch1.Id,
                UserId = userId, Sale = sale, Supply = supply, Shift = shift, CreatedAt = date
            };
            context.Transactions.Add(t);
            return t;
        }

        var supplierDebt = new Dictionary<Supplier, Account>();
        foreach (var sup in suppliers)
        {
            var acc = new Account { Supplier = sup, Name = "Qarz", Type = AccountType.Debt, CreatedAt = openingDate };
            await context.Accounts.AddAsync(acc);
            supplierDebt[sup] = acc;
        }

        Post(OperationType.CashIn, totalSupply + 4_000_000m, null, cash, admin.Id, openingDate);
        for (var i = 0; i < supplies.Count; i++)
        {
            var sup = supplies[i];
            var acc = supplierDebt[sup.Supplier!];
            Post(OperationType.DebtCharge, sup.TotalAmount, acc, null, admin.Id, sup.CreatedAt, supply: sup);
            var paid = i == supplies.Count - 1 ? 0m
                : i == supplies.Count - 2 ? R(sup.TotalAmount * 0.6m, 1000m)
                : sup.TotalAmount;
            if (paid > 0)
                Post(OperationType.SupplyPay, paid, cash, acc, admin.Id, sup.CreatedAt.AddHours(2), supply: sup);
        }

        var dayShift = new Dictionary<int, Shift>();
        for (var d = -29; d <= 0; d++)
        {
            var open = Day(d).AddHours(9);
            var sh = new Shift
            {
                BranchId = branch1.Id, UserId = seller.Id, OpeningFloat = 200_000m,
                OpenedAt = open, CreatedAt = open,
                Status = d == 0 ? ShiftStatus.Open : ShiftStatus.Closed,
                ClosedAt = d == 0 ? null : Day(d).AddHours(21).AddMinutes(30)
            };
            context.Shifts.Add(sh);
            dayShift[d] = sh;
        }

        var cashByDay = new Dictionary<int, decimal>();
        void Drawer(int d, decimal amt) => cashByDay[d] = cashByDay.GetValueOrDefault(d) + amt;

        var expenseCats = new[]
        {
            new ExpenseCategory { Name = "Ijara", CreatedAt = openingDate },
            new ExpenseCategory { Name = "Kommunal", CreatedAt = openingDate },
            new ExpenseCategory { Name = "Xo'jalik", CreatedAt = openingDate },
        };
        await context.ExpenseCategories.AddRangeAsync(expenseCats);

        decimal Remaining(long vid) => lots.TryGetValue(vid, out var l) ? l.Sum(x => x.Quantity) : 0m;

        List<SaleItem> Allocate(Sale sale, long vid, decimal qty, decimal unit)
        {
            var made = new List<SaleItem>();
            foreach (var lot in lots[vid].OrderBy(x => x.CreatedAt))
            {
                if (qty <= 0) break;
                if (lot.Quantity <= 0) continue;
                var take = Math.Min(qty, lot.Quantity);
                made.Add(new SaleItem { Sale = sale, VariantId = vid, Stock = lot, Quantity = take, UnitPrice = unit, PurchasePrice = lot.PurchasePrice });
                lot.Quantity -= take;
                qty -= take;
            }
            return made;
        }

        for (var d = -29; d <= 0; d++)
        {
            var dow = Day(d).DayOfWeek;
            var weekend = dow is DayOfWeek.Saturday or DayOfWeek.Sunday;
            var count = weekend ? rnd.Next(9, 15) : rnd.Next(5, 10);
            var shift = dayShift[d];

            for (var k = 0; k < count; k++)
            {
                var time = Day(d).AddHours(9 + rnd.Next(0, 11)).AddMinutes(rnd.Next(0, 60));
                var sale = new Sale
                {
                    BranchId = branch1.Id, WarehouseId = wh1.Id, UserId = rnd.Next(5) == 0 ? admin.Id : seller.Id,
                    Status = SaleStatus.Completed, ReceiptToken = Guid.NewGuid().ToString("N"), CreatedAt = time
                };

                var lines = rnd.Next(1, 4);
                var used = new HashSet<long>();
                var items = new List<SaleItem>();
                decimal gross = 0m;
                for (var li = 0; li < lines; li++)
                {
                    long vid = 0;
                    for (var attempt = 0; attempt < 8; attempt++)
                    {
                        var cand = allVids[rnd.Next(allVids.Count)];
                        if (used.Add(cand) && Remaining(cand) >= 1) { vid = cand; break; }
                    }
                    if (vid == 0) continue;
                    var qty = Math.Min(rnd.Next(1, 6), Remaining(vid));
                    var made = Allocate(sale, vid, qty, prices[vid]);
                    items.AddRange(made);
                    gross += made.Sum(m => m.Quantity * m.UnitPrice);
                }
                if (items.Count == 0) continue;
                sale.Items = items;

                decimal discount = 0m;
                if (rnd.Next(100) < 20)
                {
                    var pct = new[] { 5, 7, 10 }[rnd.Next(3)];
                    discount = R(gross * pct / 100m, 1000m);
                    if (discount >= gross) discount = 0m;
                }
                var net = gross - discount;
                sale.DiscountAmount = discount;
                sale.TotalAmount = net;

                var cust = rnd.Next(100) < 55 ? customers[rnd.Next(customers.Count)] : null;
                sale.Customer = cust;

                decimal payCash = 0, payCard = 0, payBonus = 0, debt = 0;
                var roll = rnd.Next(100);
                if (cust is null)
                {
                    if (roll < 60) payCash = net;
                    else if (roll < 85) payCard = net;
                    else { payCard = R(net * 0.5m, 1000m); payCash = net - payCard; }
                }
                else
                {
                    var bonusBal = bonusAcc[cust].Balance;
                    if (roll < 40) payCash = net;
                    else if (roll < 60) payCard = net;
                    else if (roll < 70) { payCard = R(net * 0.5m, 1000m); payCash = net - payCard; }
                    else if (roll < 85)
                    {
                        debt = R(net * new[] { 0.4m, 0.6m, 1m }[rnd.Next(3)], 1000m);
                        if (debt > net) debt = net;
                        payCash = net - debt;
                    }
                    else if (bonusBal >= 1000m)
                    {
                        var cap = Math.Floor(bonusBal / 1000m) * 1000m;
                        payBonus = Math.Min(cap, R(net * 0.5m, 1000m));
                        if (payBonus < 1000m) payBonus = 0m;
                        payCash = net - payBonus;
                    }
                    else payCash = net;
                }

                sale.PaidCash = payCash;
                sale.PaidCard = payCard;
                sale.PaidBonus = payBonus;
                sale.DebtAmount = debt;
                if (payCash == net && rnd.Next(100) < 25)
                {
                    var handed = Math.Ceiling(net / 10_000m) * 10_000m;
                    sale.ChangeAmount = handed - net;
                }

                var uid = sale.UserId;
                if (payCash > 0) { Post(OperationType.Sale, payCash, null, cash, uid, time, sale, shift: shift); Drawer(d, payCash); }
                if (payCard > 0) Post(OperationType.Sale, payCard, null, card, uid, time, sale, shift: shift);
                if (payBonus > 0) Post(OperationType.BonusSpend, payBonus, bonusAcc[cust!], null, uid, time, sale, shift: shift);
                if (debt > 0) Post(OperationType.DebtCharge, debt, null, debtAcc[cust!], uid, time, sale, shift: shift);

                if (cust is not null && payBonus == 0m)
                {
                    var cb = R(net * 0.01m, 100m);
                    if (cb > 0) { sale.CashbackEarned = cb; Post(OperationType.Cashback, cb, null, bonusAcc[cust], uid, time, sale, shift: shift); }
                }

                context.Sales.Add(sale);
            }

            if (d < 0 && rnd.Next(100) < 30)
            {
                var debtor = customers.FirstOrDefault(c => debtAcc[c].Balance >= 1000m);
                if (debtor is not null)
                {
                    var bal = debtAcc[debtor].Balance;
                    var pay = Math.Floor(Math.Min(bal, bal * 0.6m + 5000m) / 1000m) * 1000m;
                    if (pay >= 1000m)
                    {
                        Post(OperationType.DebtPay, pay, debtAcc[debtor], cash, admin.Id, Day(d).AddHours(12), shift: shift);
                        Drawer(d, pay);
                    }
                }
            }

            if (rnd.Next(100) < 40 && cashByDay.GetValueOrDefault(d) > 150_000m)
            {
                var amount = rnd.Next(3, 9) * 10_000m;
                var cat2 = expenseCats[rnd.Next(expenseCats.Length)];
                var expense = Post(OperationType.CashOut, amount, cash, null, seller.Id, Day(d).AddHours(14).AddMinutes(rnd.Next(0, 240)), shift: shift);
                expense.ExpenseCategory = cat2;
                expense.Description = cat2.Name;
                Drawer(d, -amount);
            }
        }

        var dueScenarios = new (int SaleDay, int DueOffset, int CustIndex)[] { (-12, -4, 0), (-6, 1, 2), (-2, 10, 4) };
        foreach (var (saleDay, dueOffset, custIndex) in dueScenarios)
        {
            var cust = customers[custIndex];
            var vid = allVids.First(v => Remaining(v) >= 3);
            var time = Day(saleDay).AddHours(16);
            var dueSale = new Sale
            {
                BranchId = branch1.Id, WarehouseId = wh1.Id, UserId = seller.Id, Customer = cust,
                Status = SaleStatus.Completed, ReceiptToken = Guid.NewGuid().ToString("N"), CreatedAt = time,
                DebtDueDate = DateOnly.FromDateTime(Day(dueOffset))
            };
            var made = Allocate(dueSale, vid, 3, prices[vid]);
            dueSale.Items = made;
            var total = made.Sum(m => m.Quantity * m.UnitPrice);
            dueSale.TotalAmount = total;
            dueSale.DebtAmount = total;
            Post(OperationType.DebtCharge, total, null, debtAcc[cust], seller.Id, time, dueSale, shift: dayShift[saleDay]);
            context.Sales.Add(dueSale);
        }

        var returnCandidates = context.ChangeTracker.Entries<Sale>()
            .Select(e => e.Entity)
            .Where(s => s.Customer is null && s.PaidCash > 0 && s.PaidCard == 0 && s.DebtAmount == 0 && s.Items.Count == 1 && s.CreatedAt >= Day(-6) && s.CreatedAt < Day(0))
            .Take(2)
            .ToList();
        foreach (var sale in returnCandidates)
        {
            var item = sale.Items.First();
            var half = Math.Floor(item.Quantity / 2m);
            var qty = half >= 1 ? half : item.Quantity;
            item.ReturnedQuantity = qty;
            var refund = Math.Round(qty * item.UnitPrice * (1 - (sale.TotalAmount > 0 ? sale.DiscountAmount / (sale.TotalAmount + sale.DiscountAmount) : 0)), 2);
            refund = Math.Min(refund, sale.PaidCash);
            var d = (int)(sale.CreatedAt.Date - today0).TotalDays;
            Post(OperationType.Sale, refund, cash, null, sale.UserId, sale.CreatedAt.AddHours(3), sale, shift: dayShift.GetValueOrDefault(d));
            sale.RefundedCash = refund;
            sale.Status = qty >= item.Quantity ? SaleStatus.Returned : SaleStatus.PartialReturn;
            item.Stock.Quantity += qty;
            Drawer(d, -refund);
        }

        context.ExchangeRates.AddRange(
            new ExchangeRate { Code = "USD", Rate = 12500m, EffectiveAt = Day(-20).AddHours(9), UserId = admin.Id },
            new ExchangeRate { Code = "USD", Rate = 12550m, EffectiveAt = Day(-10).AddHours(9), UserId = admin.Id },
            new ExchangeRate { Code = "USD", Rate = 12600m, EffectiveAt = Day(-1).AddHours(9), UserId = admin.Id },
            new ExchangeRate { Code = "RUB", Rate = 155m, EffectiveAt = Day(-1).AddHours(9), UserId = admin.Id });

        for (var d = -29; d <= -1; d++)
        {
            var sh = dayShift[d];
            var variance = rnd.Next(10) == 0 ? rnd.Next(-15, 16) * 1000m : 0m;
            sh.CountedCash = sh.OpeningFloat + cashByDay.GetValueOrDefault(d) + variance;
        }

        context.StockTransfers.AddRange(
            new StockTransfer { BranchId = branch1.Id, FromWarehouseId = wh1.Id, ToWarehouseId = wh2.Id, VariantId = allVids[0], Quantity = 10, UserId = admin.Id, Status = TransferStatus.Received, CreatedAt = Day(-12).AddHours(11) },
            new StockTransfer { BranchId = branch1.Id, FromWarehouseId = wh1.Id, ToWarehouseId = wh2.Id, VariantId = allVids[3], Quantity = 20, UserId = admin.Id, Status = TransferStatus.Sent, CreatedAt = Day(-5).AddHours(15) },
            new StockTransfer { BranchId = branch1.Id, FromWarehouseId = wh1.Id, ToWarehouseId = wh2.Id, VariantId = allVids[9], Quantity = 5, UserId = admin.Id, Status = TransferStatus.Cancelled, CreatedAt = Day(-3).AddHours(16) });

        context.BusinessSettings.Add(new BusinessSetting { Key = "demo_seeded", Value = "true", CreatedAt = cat.Now });

        await context.SaveChangesAsync();
    }
}
