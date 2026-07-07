using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Persistence.Seed;

public static class DemoDataSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        if (await context.BusinessSettings.AnyAsync(s => s.Key == "demo_seeded" && s.Value == "true"))
            return;
        if (await context.Sales.AnyAsync())
            return;

        var rnd = new Random(0xCA27EC);
        var now = DateTime.UtcNow;
        var today0 = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        DateTime Day(int offset) => today0.AddDays(offset);
        var openingDate = Day(-30).AddHours(8);

        var branch1 = await context.Branches.FirstAsync(b => b.Name == "Filial 1");
        var branch2 = await context.Branches.FirstAsync(b => b.Name == "Filial 2");
        var wh1 = await context.Warehouses.FirstAsync(w => w.BranchId == branch1.Id);
        var wh2 = await context.Warehouses.FirstAsync(w => w.BranchId == branch2.Id);
        var admin = await context.Users.FirstAsync(u => u.Username == "admin");
        var seller = await context.Users.FirstAsync(u => u.Username == "seller");
        var cash = await context.Accounts.FirstAsync(a => a.BranchId == branch1.Id && a.Type == AccountType.Cash);
        var card = await context.Accounts.FirstAsync(a => a.BranchId == branch1.Id && a.Type == AccountType.Card);

        var prices = await context.ProductPrices.Where(p => p.WarehouseId == null)
            .ToDictionaryAsync(p => p.VariantId, p => p.SellingPrice);
        var baseStocks = await context.Stocks.Where(s => s.BranchId == branch1.Id).ToListAsync();
        var purchase = baseStocks.GroupBy(s => s.VariantId).ToDictionary(g => g.Key, g => g.First().PurchasePrice);
        var allVids = prices.Keys.OrderBy(x => x).ToList();

        decimal Buy(long vid) => purchase.TryGetValue(vid, out var p) && p > 0 ? p : Math.Round(prices[vid] * 0.72m / 100m) * 100m;
        decimal R(decimal x, decimal step) => Math.Round(x / step, MidpointRounding.AwayFromZero) * step;

        var lots = new Dictionary<long, List<Stock>>();
        void AddLot(Stock s)
        {
            if (!lots.TryGetValue(s.VariantId, out var list)) lots[s.VariantId] = list = [];
            list.Add(s);
        }

        foreach (var s in baseStocks)
        {
            s.CreatedAt = openingDate;
            if (s.Quantity > 0) AddLot(s);
        }

        var suppliers = new[]
        {
            new Supplier { Name = "Mega Distribution", Phone = "+998711110011", CreatedAt = openingDate },
            new Supplier { Name = "Oziq Optom Savdo", Phone = "+998711110022", CreatedAt = openingDate },
            new Supplier { Name = "Nestle Uzbekistan", Phone = "+998711110033", CreatedAt = openingDate },
            new Supplier { Name = "Mahalliy Non Zavodi", Phone = "+998711110044", CreatedAt = openingDate },
        };
        await context.Suppliers.AddRangeAsync(suppliers);

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

        var groups = new List<long>[4] { [], [], [], [] };
        for (var i = 0; i < allVids.Count; i++) groups[i % 4].Add(allVids[i]);

        AddSupply(suppliers[0], Day(-29).AddHours(10), groups[0], 60);
        AddSupply(suppliers[1], Day(-28).AddHours(10), groups[1], 60);
        AddSupply(suppliers[2], Day(-27).AddHours(10), groups[2], 60);
        AddSupply(suppliers[3], Day(-26).AddHours(10), groups[3], 60);
        AddSupply(suppliers[0], Day(-15).AddHours(10), groups[0].Take(4), 40);
        AddSupply(suppliers[3], Day(-7).AddHours(10), groups[3].Take(4), 40);

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
        var customers = custInfo.Select(c => new Customer { FullName = c.Full, Phone = c.Phone, CardBarcode = c.Card, DiscountPct = c.Disc, CreditLimit = c.Limit, CreatedAt = openingDate }).ToList();
        await context.Customers.AddRangeAsync(customers);

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
            var acc = supplierDebt[sup.Supplier];
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
                    var qty = Math.Min((decimal)rnd.Next(1, 6), Remaining(vid));
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
                var cat = expenseCats[rnd.Next(expenseCats.Length)];
                var expense = Post(OperationType.CashOut, amount, cash, null, seller.Id, Day(d).AddHours(14).AddMinutes(rnd.Next(0, 240)), shift: shift);
                expense.ExpenseCategory = cat;
                expense.Description = cat.Name;
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

        context.BusinessSettings.Add(new BusinessSetting { Key = "demo_seeded", Value = "true", CreatedAt = now });

        await context.SaveChangesAsync();
    }
}
