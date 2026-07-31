using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.Sales.Queries;
using Cartex.Application.Settings.Queries;
using Cartex.Domain.Events;
using Cartex.Infrastructure.Notifications;

namespace Cartex.Infrastructure.CloudBridge;

public sealed class ReceiptMirrorHandler(ISender sender, IReceiptPdfRenderer pdfRenderer, CloudBridgeClient bridge)
    : INotificationHandler<DomainEventNotification<ReceiptMirrorEvent>>
{
    public async Task Handle(DomainEventNotification<ReceiptMirrorEvent> notification, CancellationToken cancellationToken)
    {
        var cfg = await bridge.GetActiveAsync(cancellationToken);
        if (cfg is null)
            return;

        var receipt = await sender.Send(new GetReceiptByTokenQuery(notification.DomainEvent.ReceiptToken), cancellationToken);
        if (receipt is null)
            return;

        var s = await sender.Send(new GetReceiptSettingsQuery(), cancellationToken);
        var opts = new ReceiptSettings
        {
            HeaderText = s.HeaderText,
            FooterText = s.FooterText,
            PaperWidth = s.PaperWidth,
            PaperFormat = s.PaperFormat,
            ShowBusinessName = s.ShowBusinessName,
            ShowBranchName = s.ShowBranchName,
            ShowAddress = s.ShowAddress,
            ShowPhone = s.ShowPhone,
            ShowCashier = s.ShowCashier,
            ShowCustomer = s.ShowCustomer,
            ShowReceiptNumber = s.ShowReceiptNumber,
            ShowPaymentDetails = s.ShowPaymentDetails,
            ShowQrCode = s.ShowQrCode,
            ShowElectronicLink = s.ShowElectronicLink,
            PublicReceiptBaseUrl = s.PublicReceiptBaseUrl
        };
        var html = ReceiptHtmlRenderer.Render(receipt, opts);
        var pdf = opts?.PaperFormat switch
        {
            "A4" => pdfRenderer.RenderDocument(receipt, opts, a4: true),
            "Thermal" => pdfRenderer.Render(receipt, opts),
            _ => pdfRenderer.RenderDocument(receipt, opts)
        };
        await bridge.PushReceiptAsync(cfg, notification.DomainEvent.ReceiptToken, html, pdf, cancellationToken);
    }
}
