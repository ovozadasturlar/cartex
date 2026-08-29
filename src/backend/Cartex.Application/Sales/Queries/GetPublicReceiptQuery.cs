using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Shared.Models.Sales;

namespace Cartex.Application.Sales.Queries;

public record GetPublicReceiptQuery(string Token) : IRequest<ReceiptDto?>;

public sealed class GetPublicReceiptQueryHandler(ISender sender, ISettingsService settings) : IRequestHandler<GetPublicReceiptQuery, ReceiptDto?>
{
    public async Task<ReceiptDto?> Handle(GetPublicReceiptQuery request, CancellationToken cancellationToken)
    {
        var receipt = await sender.Send(new GetReceiptByTokenQuery(request.Token), cancellationToken);
        if (receipt is null) return null;

        var configured = await settings.GetAsync<ReceiptSettings>(SettingKeys.Receipt, cancellationToken) ?? new ReceiptSettings();
        return receipt with
        {
            UserName = configured.ShowCashier ? receipt.UserName : "",
            CustomerName = configured.ShowCustomer ? receipt.CustomerName : null,
            CustomerPhone = configured.ShowCustomerPhone ? receipt.CustomerPhone : null,
            CustomerEmail = null,
            CustomerId = null
        };
    }
}
