using Cartex.Application.Sales.Queries;

namespace Cartex.Application.Common.Interfaces;

public interface IReceiptPdfRenderer
{
    byte[] Render(ReceiptDto receipt);
}
