using Cartex.Shared.Models.Customers;

namespace Cartex.Application.Common.Interfaces;

public sealed record GeneratedDocument(byte[] Content, string ContentType, string FileName);

public interface ICustomerStatementExporter
{
    GeneratedDocument Export(CustomerStatementDto statement, string format, string mode);
}
