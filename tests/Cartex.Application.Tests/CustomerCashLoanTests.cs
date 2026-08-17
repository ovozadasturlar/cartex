using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.CustomerPayments.Commands;
using Cartex.Application.Customers.Commands;
using Cartex.Application.Shifts.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class CustomerCashLoanTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private const decimal Advance = 200_000m;
    private const decimal Payout = 500_000m;
    private const decimal Loan = Payout - Advance;      // 500 000 - 200 000 = 300 000
    private const decimal TillFloat = 20_000_000m;

    private sealed record Setup(long Branch, long Business, long Admin);

    private sealed record Split(decimal Total, decimal FromAdvance, decimal FromLoan);

    private async Task<Setup> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return new Setup(
            (await db.Branches.FirstAsync(x => x.Name == "Asosiy filial")).Id,
            (await db.Businesses.FirstAsync()).Id,
            (await db.Users.FirstAsync(x => x.Username == "admin")).Id);
    }

    /// The drawer is filled well above every payout under test, so a payout can only be refused
    /// by the rule being tested and never by an empty till.
    private async Task AsAdminAsync(Setup s)
    {
        Fixture.CurrentUser.AsAdmin(s.Admin, s.Business, s.Branch);
        await TestShift.OpenAsync(Fixture);
        await Send(x => x.Send(new AddCashMovementCommand(TillFloat, IsPayOut: false)));
    }

    private async Task SetPolicyAsync(bool allowCustomerLoans, decimal? maxCustomerLoan = null)
    {
        using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISettingsService>()
            .SetAsync(SettingKeys.SalesPolicy, new SalesPolicySettings
            {
                AllowCustomerLoans = allowCustomerLoans,
                MaxCustomerLoan = maxCustomerLoan
            });
    }

    private async Task<T> Send<T>(Func<ISender, Task<T>> action)
    {
        using var scope = Fixture.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ISender>());
    }

    private Task<long> CreateCustomerAsync(decimal advance) =>
        Send(x => x.Send(new CreateCustomerCommand("Naqd qarz mijozi",
            "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999), null, 0m,
            CreditLimit: 100_000_000m, OpeningBalance: -advance)));

    private Task<CustomerRefundCreatedDto> PayOutAsync(Setup s, long customerId, decimal cash) =>
        Send(x => x.Send(new CreateCustomerRefundCommand(customerId, s.Branch,
            [new CustomerRefundTenderInput(PaymentMethod.Cash, "UZS", cash)],
            Note: "Mijozga naqd berildi",
            IdempotencyKey: $"cash-loan-{Guid.NewGuid():N}")));

    private Task<CustomerPaymentCreatedDto> PayInAsync(Setup s, long customerId, decimal cash) =>
        Send(x => x.Send(new CreateCustomerPaymentCommand(customerId, s.Branch,
            [new CustomerPaymentTenderInput(PaymentMethod.Cash, "UZS", cash)],
            IdempotencyKey: $"loan-repay-{Guid.NewGuid():N}")));

    private async Task<decimal> DebtAsync(long customerId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Accounts
            .Where(x => x.CustomerId == customerId && x.Type == AccountType.Debt)
            .SumAsync(x => x.Balance);
    }

    private async Task<decimal> AdvanceAsync(long customerId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Accounts
            .Where(x => x.CustomerId == customerId && x.Type == AccountType.CustomerAdvance)
            .SumAsync(x => x.Balance);
    }

    private async Task<decimal> TillAsync(long branchId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Accounts
            .Where(x => x.BranchId == branchId && x.Type == AccountType.Cash)
            .SumAsync(x => x.Balance);
    }

    private async Task<int> PayoutCountAsync(long customerId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.CustomerRefundDocuments.CountAsync(x => x.CustomerId == customerId);
    }

    private async Task<Split> SplitAsync(long refundId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.CustomerRefundDocuments
            .Where(x => x.Id == refundId)
            .Select(x => new Split(x.TotalBaseAmount, x.AdvanceBaseAmount, x.LoanBaseAmount))
            .SingleAsync();
    }

    /// Absolute value: the rule fixes which operation type carries the money and how much,
    /// the sign convention of the ledger entry is not part of the specification.
    private async Task<decimal> PostedAsync(long refundId, OperationType operation)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return Math.Abs(await db.Transactions
            .Where(x => x.CustomerRefundDocumentId == refundId && x.OperationType == operation)
            .SumAsync(x => x.Amount));
    }

    private async Task<int> PostedCountAsync(long refundId, OperationType operation)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Transactions
            .CountAsync(x => x.CustomerRefundDocumentId == refundId && x.OperationType == operation);
    }

    [Fact]
    public async Task QARZ_07_QARZ_08_Payout_over_the_advance_drains_it_first_and_lends_the_rest()
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);
        await SetPolicyAsync(allowCustomerLoans: true);

        var customerId = await CreateCustomerAsync(Advance);
        // berilgan: avans 200 000, qarz 0
        Assert.Equal(Advance, await AdvanceAsync(customerId));
        Assert.Equal(0m, await DebtAsync(customerId));
        var tillBefore = await TillAsync(s.Branch);

        var refund = await PayOutAsync(s, customerId, Payout);

        // QARZ-07: 500 000 chiqimning avvalgi 200 000 i avansdan -> avans 200 000 - 200 000 = 0
        Assert.Equal(0m, await AdvanceAsync(customerId));
        // QARZ-08: yetmagan qismi qarz -> 500 000 - 200 000 = 300 000
        Assert.Equal(Loan, await DebtAsync(customerId));
        // QARZ-10: kassadan 500 000 chiqadi
        Assert.Equal(tillBefore - Payout, await TillAsync(s.Branch));

        // QARZ-08: naqd qarz ataylab CustomerLoan bilan yoziladi, tovar qarzi (DebtCharge) bilan emas
        Assert.Equal(Loan, await PostedAsync(refund.Id, OperationType.CustomerLoan));
        Assert.Equal(0, await PostedCountAsync(refund.Id, OperationType.DebtCharge));
    }

    [Fact]
    public async Task QARZ_08_QARZ_11_Repayment_closes_the_loan_and_leaves_no_advance()
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);
        await SetPolicyAsync(allowCustomerLoans: true);

        var customerId = await CreateCustomerAsync(Advance);
        var tillBefore = await TillAsync(s.Branch);

        await PayOutAsync(s, customerId, Payout);
        // 500 000 - 200 000 = 300 000 qarz
        Assert.Equal(Loan, await DebtAsync(customerId));

        // mijoz aynan qarzini to'laydi: 300 000
        await PayInAsync(s, customerId, Loan);

        // 300 000 - 300 000 = 0
        Assert.Equal(0m, await DebtAsync(customerId));
        // mijozning o'z 200 000 i chiqimda ishlatilgan - u ikkinchi marta qaytariladigan pul emas
        Assert.Equal(0m, await AdvanceAsync(customerId));
        // QARZ-10: 500 000 chiqdi, 300 000 qaytdi -> kassa 200 000 ga kamayadi, ya'ni
        // mijozning o'ziniki bo'lgan 200 000 ga
        Assert.Equal(tillBefore - Advance, await TillAsync(s.Branch));
    }

    [Fact]
    public async Task QARZ_03_QARZ_08_Repayment_above_the_loan_becomes_a_new_advance()
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);
        await SetPolicyAsync(allowCustomerLoans: true);

        var customerId = await CreateCustomerAsync(Advance);
        var tillBefore = await TillAsync(s.Branch);

        await PayOutAsync(s, customerId, Payout);
        // 500 000 - 200 000 = 300 000 qarz
        Assert.Equal(Loan, await DebtAsync(customerId));

        // 300 000 qarzga qarshi 500 000 to'laydi
        await PayInAsync(s, customerId, Payout);

        // to'lov avval qarzni yopadi: 300 000 - 300 000 = 0
        Assert.Equal(0m, await DebtAsync(customerId));
        // ortgan 500 000 - 300 000 = 200 000 yo'qolmaydi, yangi avans bo'lib yoziladi
        Assert.Equal(Advance, await AdvanceAsync(customerId));
        // QARZ-10: 500 000 chiqdi, 500 000 qaytib kirdi -> kassa boshlang'ich holatida
        Assert.Equal(tillBefore, await TillAsync(s.Branch));
    }

    [Fact]
    public async Task QARZ_09_Payout_over_the_advance_is_refused_while_lending_is_off()
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);
        // SOZ-01: siyosat umuman yozilmagan -> tizim standart bo'yicha ishlaydi,
        // AllowCustomerLoans standarti - yopiq

        var customerId = await CreateCustomerAsync(Advance);
        var tillBefore = await TillAsync(s.Branch);

        await Assert.ThrowsAsync<BusinessRuleException>(() => PayOutAsync(s, customerId, Payout));

        // hech narsa yozilmaydi: hujjat ham, pul harakati ham yo'q
        Assert.Equal(0, await PayoutCountAsync(customerId));
        Assert.Equal(Advance, await AdvanceAsync(customerId));
        Assert.Equal(0m, await DebtAsync(customerId));
        Assert.Equal(tillBefore, await TillAsync(s.Branch));
    }

    [Fact]
    public async Task QARZ_09_Lending_without_the_customers_loan_permission_is_forbidden()
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);
        await SetPolicyAsync(allowCustomerLoans: true);

        var customerId = await CreateCustomerAsync(Advance);
        var tillBefore = await TillAsync(s.Branch);

        // avansdan pul qaytara oladigan, lekin kassadan qarzga pul chiqara olmaydigan kassir
        Fixture.CurrentUser.AsCashier(s.Admin, s.Business, s.Branch);
        Fixture.CurrentUser.Granted.Add(AppPermissions.Customers.Refund);
        Fixture.CurrentUser.Granted.Add(AppPermissions.Customers.View);
        Fixture.CurrentUser.Granted.Add(AppPermissions.Rates.View);
        // Faqat sozlash uchun: mijozni admin yaratgan, busiz chaqiruv tekshiruvga yetib bormay
        // "topilmadi" bo'lib tugaydi.
        Fixture.CurrentUser.Granted.Add(AppPermissions.Customers.ViewAll);

        await Assert.ThrowsAsync<ForbiddenException>(() => PayOutAsync(s, customerId, Payout));
        Assert.Equal(0, await PayoutCountAsync(customerId));
        Assert.Equal(Advance, await AdvanceAsync(customerId));
        Assert.Equal(tillBefore, await TillAsync(s.Branch));

        // qo'riqchi faqat qarz qismiga tegishli: avans bilan to'liq qoplangan 200 000 o'tadi
        await PayOutAsync(s, customerId, Advance);
        Assert.Equal(0m, await AdvanceAsync(customerId));
        Assert.Equal(0m, await DebtAsync(customerId));

        // va ruxsat berilgach, o'sha qarz chiqimi qabul qilinadi
        Fixture.CurrentUser.Granted.Add(AppPermissions.Customers.Loan);
        await PayOutAsync(s, customerId, Loan);
        // avans 0 edi -> 300 000 ning hammasi qarz
        Assert.Equal(Loan, await DebtAsync(customerId));
        // 200 000 + 300 000 = 500 000 kassadan chiqdi
        Assert.Equal(tillBefore - Payout, await TillAsync(s.Branch));
    }

    [Fact]
    public async Task QARZ_09_Loan_over_the_ceiling_is_refused_and_the_ceiling_itself_is_accepted()
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);
        // egasi bitta chiqimda ko'pi bilan 300 000 qarz berishga ruxsat berdi
        await SetPolicyAsync(allowCustomerLoans: true, maxCustomerLoan: Loan);

        var overId = await CreateCustomerAsync(Advance);
        var tillBefore = await TillAsync(s.Branch);

        // 550 000 - 200 000 avans = 350 000 qarz, chegaradan 50 000 ko'p
        await Assert.ThrowsAsync<BusinessRuleException>(() => PayOutAsync(s, overId, 550_000m));
        Assert.Equal(0, await PayoutCountAsync(overId));
        Assert.Equal(Advance, await AdvanceAsync(overId));
        Assert.Equal(tillBefore, await TillAsync(s.Branch));

        // 500 000 - 200 000 avans = 300 000 qarz, chegaraning o'zi - qabul qilinadi
        var exactId = await CreateCustomerAsync(Advance);
        await PayOutAsync(s, exactId, Payout);
        Assert.Equal(Loan, await DebtAsync(exactId));
        Assert.Equal(0m, await AdvanceAsync(exactId));
        Assert.Equal(tillBefore - Payout, await TillAsync(s.Branch));
    }

    [Fact]
    public async Task SOZ_02_An_empty_MaxCustomerLoan_means_no_ceiling()
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);
        await SetPolicyAsync(allowCustomerLoans: true, maxCustomerLoan: null);

        var customerId = await CreateCustomerAsync(advance: 0m);
        var tillBefore = await TillAsync(s.Branch);

        // bo'sh chegara - "chegara yo'q": avanssiz 10 000 000 ham o'tadi
        await PayOutAsync(s, customerId, 10_000_000m);

        Assert.Equal(10_000_000m, await DebtAsync(customerId));
        Assert.Equal(0m, await AdvanceAsync(customerId));
        Assert.Equal(tillBefore - 10_000_000m, await TillAsync(s.Branch));
    }

    /// SOZ-02: nol chegara endi "qarzga berish yopiq" degani.
    [Fact]
    public async Task SOZ_02_A_zero_MaxCustomerLoan_closes_lending()
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);
        await SetPolicyAsync(allowCustomerLoans: true, maxCustomerLoan: 0m);

        var customerId = await CreateCustomerAsync(advance: 0m);

        await Assert.ThrowsAnyAsync<Exception>(() => PayOutAsync(s, customerId, 100_000m));
    }

    [Fact]
    public async Task QARZ_07_Payout_inside_the_advance_creates_no_debt_and_needs_no_loan_permission()
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);
        // qarz berish yopiq turgan holatda ham sof avans chiqimi ishlashda davom etadi

        var customerId = await CreateCustomerAsync(Advance);
        var tillBefore = await TillAsync(s.Branch);

        Fixture.CurrentUser.AsCashier(s.Admin, s.Business, s.Branch);
        Fixture.CurrentUser.Granted.Add(AppPermissions.Customers.Refund);
        Fixture.CurrentUser.Granted.Add(AppPermissions.Customers.View);
        Fixture.CurrentUser.Granted.Add(AppPermissions.Customers.ViewAll);
        Fixture.CurrentUser.Granted.Add(AppPermissions.Rates.View);

        await PayOutAsync(s, customerId, 150_000m);

        // 200 000 - 150 000 = 50 000 avans qoladi, qarz umuman tug'ilmaydi
        Assert.Equal(50_000m, await AdvanceAsync(customerId));
        Assert.Equal(0m, await DebtAsync(customerId));
        Assert.Equal(tillBefore - 150_000m, await TillAsync(s.Branch));
    }

    [Fact]
    public async Task QARZ_10_Cash_payout_without_an_open_shift_is_refused()
    {
        var s = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(s.Admin, s.Business, s.Branch);
        await SetPolicyAsync(allowCustomerLoans: true);

        var customerId = await CreateCustomerAsync(Advance);
        var tillBefore = await TillAsync(s.Branch);

        await Assert.ThrowsAsync<BusinessRuleException>(() => PayOutAsync(s, customerId, Payout));

        Assert.Equal(0, await PayoutCountAsync(customerId));
        Assert.Equal(Advance, await AdvanceAsync(customerId));
        Assert.Equal(0m, await DebtAsync(customerId));
        Assert.Equal(tillBefore, await TillAsync(s.Branch));
    }

    [Theory]
    [InlineData(200_000, 200_000, 0)]
    [InlineData(500_000, 200_000, 300_000)]
    public async Task QARZ_08_Document_records_how_the_payout_was_funded(
        decimal payout, decimal fromAdvance, decimal fromLoan)
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);
        await SetPolicyAsync(allowCustomerLoans: true);

        var customerId = await CreateCustomerAsync(Advance);
        var refund = await PayOutAsync(s, customerId, payout);

        // 200 000 avansli mijoz: 200 000 chiqimda qarz yo'q, 500 000 chiqimda 300 000 qarz
        var split = await SplitAsync(refund.Id);
        Assert.Equal(payout, split.Total);
        Assert.Equal(fromAdvance, split.FromAdvance);
        Assert.Equal(fromLoan, split.FromLoan);
        Assert.Equal(split.Total, split.FromAdvance + split.FromLoan);
    }
}
