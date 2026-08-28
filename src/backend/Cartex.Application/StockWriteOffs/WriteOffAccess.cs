using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;

namespace Cartex.Application.StockWriteOffs;

internal static class WriteOffAccess
{
    public static async Task EnsureTrackedAsync(ISettingsService settings, CancellationToken cancellationToken)
    {
        var policy = await settings.GetAsync<SalesPolicySettings>(SettingKeys.SalesPolicy, cancellationToken)
            ?? new SalesPolicySettings();
        if (!policy.TrackWriteOff)
            throw new ForbiddenException("Chiqim hisobi do'kon siyosatida yopilgan.", "write_off_disabled");
    }

    public static async Task EnsureAllowedAsync(ICurrentUser currentUser, ISettingsService settings, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.Stocks.WriteOff))
            throw new ForbiddenException("Chiqim qilishga ruxsat yo'q.");
        await EnsureTrackedAsync(settings, cancellationToken);
    }
}
