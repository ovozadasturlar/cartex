using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Common.Sales;

/// <summary>
/// Decides how long a posted sale may still be corrected. The window is a business
/// setting because shops differ: some close a shift every day, some never open one.
/// Shared by the void command and the UI so both always agree.
/// </summary>
public interface ISaleCorrectionPolicy
{
    Task<bool> CanCorrectAsync(long saleId, CancellationToken cancellationToken);
    Task EnsureCanCorrectAsync(Sale sale, CancellationToken cancellationToken);
}

public sealed class SaleCorrectionPolicy(
    IApplicationDbContext db,
    ISettingsService settings) : ISaleCorrectionPolicy
{
    public async Task<bool> CanCorrectAsync(long saleId, CancellationToken cancellationToken)
    {
        var sale = await db.Sales.AsNoTracking()
            .Where(x => x.Id == saleId)
            .Select(x => new { x.Id, x.Status, x.ShiftId, x.CreatedAt })
            .FirstOrDefaultAsync(cancellationToken);
        if (sale is null || sale.Status != SaleStatus.Completed)
            return false;
        if (await db.CustomerReturnLines.AnyAsync(x => x.SaleId == saleId, cancellationToken))
            return false;
        if (await db.CustomerPaymentAllocations.AnyAsync(x => x.SaleId == saleId, cancellationToken))
            return false;

        var policy = await LoadAsync(cancellationToken);
        return await IsWithinWindowAsync(policy, sale.ShiftId, sale.CreatedAt, cancellationToken) is null;
    }

    public async Task EnsureCanCorrectAsync(Sale sale, CancellationToken cancellationToken)
    {
        var policy = await LoadAsync(cancellationToken);
        if (await IsWithinWindowAsync(policy, sale.ShiftId, sale.CreatedAt, cancellationToken) is { } failure)
            throw new BusinessRuleException(failure.Message, failure.Code);
    }

    private async Task<SalesPolicySettings> LoadAsync(CancellationToken cancellationToken) =>
        await settings.GetAsync<SalesPolicySettings>(SettingKeys.SalesPolicy, cancellationToken)
        ?? new SalesPolicySettings();

    private async Task<(string Message, string Code)?> IsWithinWindowAsync(
        SalesPolicySettings policy,
        long? shiftId,
        DateTime createdAt,
        CancellationToken cancellationToken)
    {
        switch (policy.SaleCorrectionWindow)
        {
            case "Off":
                return ("Savdoni tuzatish sozlamalarda o'chirilgan.", "sale_correction_disabled");

            case "Always":
                return null;

            case "Days":
                return createdAt >= DateTime.UtcNow.AddDays(-Math.Max(0, policy.SaleCorrectionDays))
                    ? null
                    : ($"Tuzatish muddati o'tgan ({policy.SaleCorrectionDays} kun).", "sale_correction_expired");

            case "BusinessDay":
                return SameDay(createdAt);

            default:
                // Shift-based, with a business-day fallback for shops that never open a shift.
                if (shiftId is not { } id)
                    return SameDay(createdAt);
                return await db.Shifts.AnyAsync(x => x.Id == id && x.Status == ShiftStatus.Open, cancellationToken)
                    ? null
                    : ("Smena yopilgan — bu savdoni tuzatib bo'lmaydi.", "sale_correction_shift_closed");
        }
    }

    private static (string Message, string Code)? SameDay(DateTime createdAt) =>
        DateOnly.FromDateTime(createdAt) == DateOnly.FromDateTime(DateTime.UtcNow)
            ? null
            : ("Tuzatish faqat savdo qilingan kun ichida mumkin.", "sale_correction_day_over");
}
