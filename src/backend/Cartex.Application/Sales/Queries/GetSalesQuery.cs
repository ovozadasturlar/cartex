using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Application.Common.Settings;
using Cartex.Persistence;
using Cartex.Domain.Common;
using Cartex.Domain.Authorization;
using Cartex.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.Sales;

namespace Cartex.Application.Sales.Queries;

public record GetSalesQuery : FilteringRequest, IRequest<IReadOnlyCollection<SaleDto>>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public long? WarehouseId { get; set; }
    public long? CustomerId { get; set; }
}

public sealed class GetSalesQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ISettingsService settings,
    IPagingMetadataWriter writer) : IRequestHandler<GetSalesQuery, IReadOnlyCollection<SaleDto>>
{
    public async Task<IReadOnlyCollection<SaleDto>> Handle(GetSalesQuery request, CancellationToken cancellationToken)
    {
        var notificationSettings =
            await settings.GetAsync<NotificationSettings>(SettingKeys.Notification, cancellationToken);
        var canSendTelegram = notificationSettings?.Channels.Contains(NotificationChannel.Telegram) == true;
        var canSendEmail = notificationSettings?.Channels.Contains(NotificationChannel.Email) == true;
        var canSendSms = notificationSettings?.Channels.Contains(NotificationChannel.Sms) == true;

        var query = db.Sales
            .AsNoTracking()
            .ApplySaleScope(request, currentUser, request.FromDate, request.ToDate,
                request.WarehouseId, request.CustomerId);
        var paging = request with { };
        paging.WithoutSearch();

        return await query
            .ToPagedListAsync(paging,
                s => new SaleDto(
                    s.Id,
                    s.CreatedAt,
                    s.TotalAmount,
                    s.PaidCash,
                    s.PaidCard,
                    s.PaidBonus,
                    s.DebtAmount,
                    s.CreditAmount,
                    s.Status.ToString(),
                    s.ReceiptToken,
                    s.Customer != null ? s.Customer.Party.FullName : null,
                    s.User.FullName,
                    s.Items.Select(i => new SaleLineDto(
                        i.Id,
                        i.Variant.Product.Name,
                        i.Quantity,
                        i.ReturnedQuantity,
                        i.UnitPrice)).ToList(),
                    s.Customer != null &&
                    !s.Customer.NotificationsOptOut &&
                    ((canSendTelegram && s.Customer.TelegramChatId != null && s.Customer.TelegramChatId != "") ||
                     (canSendEmail && s.Customer.Party.Email != null && s.Customer.Party.Email != "") ||
                     (canSendSms && s.Customer.Party.Phone != null && s.Customer.Party.Phone != "")),
                    s.PaidAdvance),
                writer, cancellationToken);
    }
}
