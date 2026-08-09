using Cartex.Shared.Models.TradeCases;
using Cartex.Shared.Models.Customers;

namespace Cartex.Application.Common.Interfaces;

public sealed record GeneratedDocument(byte[] Content, string ContentType, string FileName);

public interface ITradeCaseStatementExporter
{
    GeneratedDocument Export(TradeCaseStatementDto statement, string format, string mode);
}

public interface ICustomerStatementExporter
{
    GeneratedDocument Export(CustomerStatementDto statement, string format, string mode);
}
