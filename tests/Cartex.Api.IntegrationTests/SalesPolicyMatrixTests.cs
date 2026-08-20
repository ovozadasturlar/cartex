using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cartex.Shared.Models.Business;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.Products;
using Cartex.Shared.Models.Sales;
using Cartex.Shared.Models.Settings;
using Cartex.Shared.Models.Supplies;
using Cartex.Shared.Models.Units;
using Cartex.Shared.Models.Warehouses;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class SalesPolicyMatrixTests(CartexApiFactory factory)
{
    private const decimal CatalogPrice = 100_000m;
    private const decimal OpeningDebt = 60_000m;
    private const decimal SaleCash = 40_000m;
    private const decimal Overpayment = 40_000m;
    private const decimal SaleDebt = CatalogPrice - SaleCash;

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Debt_and_credit_switches_govern_sale_and_payment(bool allowDebtSales, bool allowCustomerCredit)
    {
        var admin = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        await AuthHelper.EnsureOpenShiftAsync(admin);
        var currency = (await admin.GetFromJsonAsync<BusinessDto>("/api/business"))!.Currency;
        var warehouseId = (await admin.GetFromJsonAsync<List<WarehouseDto>>("/api/warehouses"))![0].Id;
        var unitId = (await admin.GetFromJsonAsync<List<UnitDto>>("/api/units"))!.First(u => u.ShortName == "dona").Id;
        var policy = await admin.GetFromJsonAsync<SalesPolicyDto>("/api/settings/sales-policy");

        var tag = Guid.NewGuid().ToString("N")[..8];
        var variantId = await CreateStockedProductAsync(admin, $"Siyosat matritsasi {tag}", unitId, warehouseId);
        var customerId = await CreateCustomerAsync(admin, $"Siyosat mijozi {tag}");

        try
        {
            (await admin.PutAsJsonAsync("/api/settings/sales-policy", policy! with
            {
                AllowDebtSales = allowDebtSales,
                AllowCustomerCredit = allowCustomerCredit
            })).EnsureSuccessStatusCode();

            var sale = await admin.PostAsJsonAsync("/api/sales", new CreateSaleRequest(
                warehouseId, customerId, SaleCash, 0m, 0m, [new CreateSaleItemRequest(variantId, 1m)])
            {
                ApplyAutoDiscount = false,
                UseCustomerAdvance = false,
                DebtDueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7)
            });

            if (allowDebtSales)
            {
                // QARZ-02: siyosat yoqilganda to'lanmagan qoldiq qarzga yoziladi (QARZ-01 defteri).
                sale.EnsureSuccessStatusCode();
                Assert.Equal(OpeningDebt + SaleDebt, await DebtAsync(admin, customerId));
            }
            else
            {
                // QARZ-02: siyosat o'chiq bo'lsa qarzga savdo serverda rad etiladi (SOZ-03, SOZ-10).
                Assert.Equal(HttpStatusCode.BadRequest, sale.StatusCode);
                Assert.Equal(OpeningDebt, await DebtAsync(admin, customerId));
            }

            var debt = await DebtAsync(admin, customerId);
            var payment = await admin.PostAsJsonAsync("/api/customer-payments", new CreateCustomerPaymentRequest(
                customerId, null, [new CustomerPaymentTenderRequest("Cash", currency, debt + Overpayment)]));

            if (allowCustomerCredit)
            {
                // QARZ-20: haqdorlik yoqilgan bo'lsa ortiqcha summa QARZ-03 bo'yicha avansga tushadi.
                payment.EnsureSuccessStatusCode();
                var created = await payment.Content.ReadFromJsonAsync<CustomerPaymentCreatedDto>();
                Assert.Equal(debt, created!.AllocatedBaseAmount);
                Assert.Equal(Overpayment, created.AdvanceBaseAmount);
                var customer = await GetCustomerAsync(admin, customerId);
                Assert.Equal(0m, customer.DebtBalance);
                Assert.Equal(Overpayment, customer.CreditBalance); // QARZ-11: haqdor holati
            }
            else
            {
                // QARZ-20: haqdorlik o'chiq bo'lsa onlayn to'lovda qarzdan ortiq summa qabul qilinmaydi.
                Assert.Equal(HttpStatusCode.BadRequest, payment.StatusCode);
                using var problem = JsonDocument.Parse(await payment.Content.ReadAsStringAsync());
                Assert.Equal("payment_exceeds_debt", problem.RootElement.GetProperty("code").GetString());
                Assert.Equal(debt, await DebtAsync(admin, customerId));
            }
        }
        finally
        {
            (await admin.PutAsJsonAsync("/api/settings/sales-policy", policy!)).EnsureSuccessStatusCode();
        }
    }

    private static async Task<long> CreateStockedProductAsync(HttpClient client, string name, long unitId, long warehouseId)
    {
        var response = await client.PostAsJsonAsync("/api/products",
            new CreateProductRequest(name, null, unitId, 0m, null, SellingPrice: CatalogPrice));
        response.EnsureSuccessStatusCode();
        var productId = await response.Content.ReadFromJsonAsync<long>();
        var products = await client.GetFromJsonAsync<List<ProductDto>>(
            $"/api/products?search={Uri.EscapeDataString(name)}");
        var variantId = products!.Single(p => p.Id == productId).DefaultVariantId;

        (await client.PostAsJsonAsync("/api/supplies", new CreateSupplyRequest(
            null,
            warehouseId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            [new CreateSupplyItemRequest(variantId, 5m, 60_000m, null)]))).EnsureSuccessStatusCode();

        return variantId;
    }

    private static async Task<long> CreateCustomerAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/customers", new CreateCustomerRequest(
            name,
            "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999),
            null,
            0m,
            CreditLimit: 10_000_000m,
            OpeningBalance: OpeningDebt));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<long>();
    }

    private static async Task<CustomerDto> GetCustomerAsync(HttpClient client, long id) =>
        (await client.GetFromJsonAsync<CustomerDto>($"/api/customers/{id}"))!;

    private static async Task<decimal> DebtAsync(HttpClient client, long id) =>
        (await GetCustomerAsync(client, id)).DebtBalance;
}
