using System.Net;
using System.Net.Http.Json;
using Cartex.Shared.Models.Business;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.Products;
using Cartex.Shared.Models.Sales;
using Cartex.Shared.Models.Settings;
using Cartex.Shared.Models.Shifts;
using Cartex.Shared.Models.Stocks;
using Cartex.Shared.Models.Supplies;
using Cartex.Shared.Models.Units;
using Cartex.Shared.Models.Warehouses;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class ShopDayChainTests(CartexApiFactory factory)
{
    private const decimal CatalogPrice = 100_000m;
    private const decimal PurchasePrice = 60_000m;
    private const decimal Supplied = 10m;
    private const decimal Sold = 3m;
    private const decimal Returned = 1m;
    private const decimal SaleCash = 120_000m;
    private const decimal DebtPayment = 80_000m;
    private const decimal SaleTotal = CatalogPrice * Sold;
    private const decimal SaleDebt = SaleTotal - SaleCash;
    private const decimal RefundAmount = CatalogPrice * Returned;

    [Fact]
    public async Task Shop_day_chain_keeps_stock_debt_and_cash_in_agreement()
    {
        var admin = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        var currency = (await admin.GetFromJsonAsync<BusinessDto>("/api/business"))!.Currency;
        var warehouseId = (await admin.GetFromJsonAsync<List<WarehouseDto>>("/api/warehouses"))![0].Id;
        var unitId = (await admin.GetFromJsonAsync<List<UnitDto>>("/api/units"))!.First(u => u.ShortName == "dona").Id;
        var policy = await admin.GetFromJsonAsync<SalesPolicyDto>("/api/settings/sales-policy");

        var tag = Guid.NewGuid().ToString("N")[..8];
        var productName = $"Kun zanjiri {tag}";
        var variantId = await CreateProductAsync(admin, productName, unitId);
        var customerId = await CreateCustomerAsync(admin, $"Kun zanjiri mijoz {tag}");

        try
        {
            // QARZ-02: qarzga savdo AllowDebtSales siyosatiga bo'ysunadi — zanjir uni yoqilgan holda o'tadi.
            (await admin.PutAsJsonAsync("/api/settings/sales-policy", policy! with { AllowDebtSales = true }))
                .EnsureSuccessStatusCode();

            await AuthHelper.EnsureNoOpenShiftAsync(admin);
            (await admin.PostAsJsonAsync("/api/shifts/open", new OpenShiftRequest(0m))).EnsureSuccessStatusCode();
            var shiftId = (await admin.GetFromJsonAsync<CurrentShiftDto>("/api/shifts/current"))!.Id;

            Assert.Equal(0m, await StockAsync(admin, warehouseId, variantId, productName));

            var supply = await admin.PostAsJsonAsync("/api/supplies", new CreateSupplyRequest(
                null,
                warehouseId,
                DateOnly.FromDateTime(DateTime.UtcNow),
                [new CreateSupplyItemRequest(variantId, Supplied, PurchasePrice, null)]));
            supply.EnsureSuccessStatusCode();

            // §13 "Ta'minot va ta'minotchi qarzi" ⬜ — kirim qoldiqni oshirishi uchun qoida ID'si yozilmagan.
            Assert.Equal(Supplied, await StockAsync(admin, warehouseId, variantId, productName));

            var sale = await admin.PostAsJsonAsync("/api/sales", new CreateSaleRequest(
                warehouseId, customerId, SaleCash, 0m, 0m, [new CreateSaleItemRequest(variantId, Sold)])
            {
                ApplyAutoDiscount = false,
                DebtDueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7)
            });
            sale.EnsureSuccessStatusCode();
            var saleId = (await sale.Content.ReadFromJsonAsync<CreateSaleResult>())!.SaleId;
            var detail = await admin.GetFromJsonAsync<SaleDetailDto>($"/api/sales/{saleId}");

            Assert.Equal(0m, detail!.DiscountAmount);              // CHEG-02
            Assert.Equal(SaleTotal, detail.TotalAmount);           // CHEG-08: Jami = brutto − chegirma
            Assert.Equal(SaleCash, detail.PaidCash);
            Assert.Equal(SaleDebt, detail.DebtAmount);             // QARZ-02: to'lanmagan qoldiq qarzga yoziladi
            var line = Assert.Single(detail.Items);
            Assert.Equal(0m, line.DiscountAmount);                 // CHEG-03
            Assert.Equal(SaleTotal, line.NetTotal);                // CHEG-03: sof = miqdor × narx − chegirma
            Assert.Equal(SaleDebt, (await GetCustomerAsync(admin, customerId)).DebtBalance); // QARZ-01

            // §13 "Ta'minot..." / OFF-14 — savdo qoldiqni kamaytirishi uchun alohida qoida ID'si yo'q.
            Assert.Equal(Supplied - Sold, await StockAsync(admin, warehouseId, variantId, productName));

            var payment = await admin.PostAsJsonAsync("/api/customer-payments", new CreateCustomerPaymentRequest(
                customerId, null, [new CustomerPaymentTenderRequest("Cash", currency, DebtPayment)]));
            payment.EnsureSuccessStatusCode();
            var paid = await payment.Content.ReadFromJsonAsync<CustomerPaymentCreatedDto>();

            Assert.False(string.IsNullOrWhiteSpace(paid!.DocumentNumber)); // HUJJ-01, HUJJ-02
            Assert.Equal(DebtPayment, paid.AllocatedBaseAmount);           // QARZ-03: to'lov avval qarzni yopadi
            Assert.Equal(0m, paid.AdvanceBaseAmount);                      // QARZ-03: ortiqcha yo'q — avans ham yo'q
            Assert.Equal(SaleDebt - DebtPayment, (await GetCustomerAsync(admin, customerId)).DebtBalance);

            var ret = await admin.PostAsJsonAsync("/api/customer-returns", new CreateCustomerReturnRequest(
                warehouseId,
                [new CustomerReturnLineRequest(variantId, Returned, line.SaleItemId, Reason: "Zanjir testi")],
                customerId));
            ret.EnsureSuccessStatusCode();
            var refund = await ret.Content.ReadFromJsonAsync<CustomerReturnCreatedDto>();

            Assert.False(string.IsNullOrWhiteSpace(refund!.DocumentNumber)); // HUJJ-01, HUJJ-02
            Assert.Equal(RefundAmount, refund.RefundAmount);                 // QAYT-01: qatorning sof qiymatidan ulush
            // QAYT-07: sharshara qarz → bonus → karta → naqd; savdoning qolgan qarzi aynan shu summani yutadi.
            Assert.Equal(0m, (await GetCustomerAsync(admin, customerId)).DebtBalance);
            // QAYT-03: qaytarilgan tovar baribir omborga qaytadi.
            Assert.Equal(Supplied - Sold + Returned, await StockAsync(admin, warehouseId, variantId, productName));

            var afterReturn = await admin.GetFromJsonAsync<SaleDetailDto>($"/api/sales/{saleId}");
            Assert.Equal("PartialReturn", afterReturn!.Status);                      // HIS-01
            Assert.Equal(Returned, afterReturn.Items[0].ReturnedQuantity);
            Assert.Equal(Sold - Returned, afterReturn.Items[0].ReturnableQuantity);  // QAYT-06

            // QAYT-06: qolgan miqdordan ortiq qaytarish qabul qilinmaydi.
            var excess = await admin.PostAsJsonAsync("/api/customer-returns", new CreateCustomerReturnRequest(
                warehouseId,
                [new CustomerReturnLineRequest(variantId, Sold, line.SaleItemId, Reason: "Zanjir testi")],
                customerId));
            Assert.Equal(HttpStatusCode.BadRequest, excess.StatusCode);
            Assert.Equal(Supplied - Sold + Returned, await StockAsync(admin, warehouseId, variantId, productName));

            var expectedCash = SaleCash + DebtPayment;
            var close = await admin.PostAsJsonAsync($"/api/shifts/{shiftId}/close", new CloseShiftRequest(expectedCash));
            close.EnsureSuccessStatusCode();
            var report = await close.Content.ReadFromJsonAsync<ZReportDto>();

            // SMENA-03: kutilgan naqd = boshlang'ich qoldiq + naqd savdo − naqd qaytim + kassa kirimi
            // − kassa chiqimi + naqd qarz to'lovi − naqd ta'minot to'lovi.
            // Zanjirda: 0 + SaleCash − 0 + 0 − 0 + DebtPayment − 0.
            Assert.Equal(SaleCash, report!.CashSales);
            Assert.Equal(0m, report.CashReturns);
            Assert.Equal(DebtPayment, report.DebtPayIn); // SMENA-07: naqd qarz to'lovi o'z ustunida
            Assert.Equal(0m, report.PayIn);              // SMENA-07: kassa kirimiga aralashmaydi
            Assert.Equal(expectedCash, report.ExpectedCash);
            Assert.Equal(0m, report.Difference); // SMENA-04: sanalgan bilan kutilganning farqi
        }
        finally
        {
            (await admin.PutAsJsonAsync("/api/settings/sales-policy", policy!)).EnsureSuccessStatusCode();
        }
    }

    private static async Task<long> CreateProductAsync(HttpClient client, string name, long unitId)
    {
        var response = await client.PostAsJsonAsync("/api/products",
            new CreateProductRequest(name, null, unitId, 0m, null, SellingPrice: CatalogPrice));
        response.EnsureSuccessStatusCode();
        var productId = await response.Content.ReadFromJsonAsync<long>();
        var products = await client.GetFromJsonAsync<List<ProductDto>>(
            $"/api/products?search={Uri.EscapeDataString(name)}");
        return products!.Single(p => p.Id == productId).DefaultVariantId;
    }

    private static async Task<long> CreateCustomerAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/customers", new CreateCustomerRequest(
            name,
            "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999),
            null,
            0m,
            CreditLimit: 10_000_000m));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<long>();
    }

    private static async Task<CustomerDto> GetCustomerAsync(HttpClient client, long id) =>
        (await client.GetFromJsonAsync<CustomerDto>($"/api/customers/{id}"))!;

    private static async Task<decimal> StockAsync(HttpClient client, long warehouseId, long variantId, string search)
    {
        var rows = await client.GetFromJsonAsync<List<StockDto>>(
            $"/api/stocks?warehouseId={warehouseId}&search={Uri.EscapeDataString(search)}");
        return rows!.Where(r => r.VariantId == variantId).Sum(r => r.Quantity);
    }
}
