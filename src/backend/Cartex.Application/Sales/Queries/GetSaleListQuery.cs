using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.Sales;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Sales.Queries;

public sealed record GetSaleListQuery : FilteringRequest, IRequest<IReadOnlyCollection<SaleListDto>>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public long? WarehouseId { get; set; }
    public long? CustomerId { get; set; }
}

public sealed class GetSaleListQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ISettingsService settings,
    IPagingMetadataWriter writer) : IRequestHandler<GetSaleListQuery, IReadOnlyCollection<SaleListDto>>
{
    public async Task<IReadOnlyCollection<SaleListDto>> Handle(
        GetSaleListQuery request,
        CancellationToken cancellationToken)
    {
        var notification = await settings.GetAsync<NotificationSettings>(
            SettingKeys.Notification, cancellationToken);
        var telegram = notification?.Channels.Contains(NotificationChannel.Telegram) == true;
        var email = notification?.Channels.Contains(NotificationChannel.Email) == true;
        var sms = notification?.Channels.Contains(NotificationChannel.Sms) == true;

        var query = db.Sales
            .AsNoTracking()
            .ApplySaleScope(request, currentUser, request.FromDate, request.ToDate,
                request.WarehouseId, request.CustomerId);

        // Search is handled explicitly across customer, cashier and product relations.
        // Generic filters/sorting/paging still apply to scalar Sale columns.
        var paging = request with { };
        paging.WithoutSearch();
        return await query.ToPagedListAsync(
            paging,
            s => new SaleListDto(
                s.Id,
                s.CreatedAt,
                s.TotalAmount,
                s.PaidCash,
                s.PaidCard,
                s.PaidBonus,
                s.PaidAdvance,
                s.DebtAmount,
                s.CreditAmount,
                s.Status.ToString(),
                s.ReceiptToken,
                s.CustomerId,
                s.Customer != null ? s.Customer.FullName : null,
                s.User.FullName,
                s.Items.Count,
                s.Items.OrderBy(i => i.Id).Select(i => i.Variant.Product.Name).FirstOrDefault(),
                s.Items.OrderBy(i => i.Id).Select(i => i.Variant.Product.Name).Skip(1).FirstOrDefault(),
                s.Customer != null &&
                !s.Customer.NotificationsOptOut &&
                ((telegram && s.Customer.TelegramChatId != null && s.Customer.TelegramChatId != "") ||
                 (email && s.Customer.Email != null && s.Customer.Email != "") ||
                 (sms && s.Customer.Phone != null && s.Customer.Phone != ""))),
            writer,
            cancellationToken);
    }
}
