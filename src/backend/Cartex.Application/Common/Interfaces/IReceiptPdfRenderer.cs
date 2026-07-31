using Cartex.Application.Common.Settings;
using Cartex.Application.Sales.Queries;

namespace Cartex.Application.Common.Interfaces;

public interface IReceiptPdfRenderer
{
    byte[] Render(ReceiptDto receipt, ReceiptSettings? settings = null);
    byte[] RenderDocument(ReceiptDto receipt, ReceiptSettings? settings = null, bool a4 = false);
    IReadOnlyList<byte[]> RenderDocumentImages(ReceiptDto receipt, ReceiptSettings? settings = null, bool a4 = false, bool landscape = false);
}
