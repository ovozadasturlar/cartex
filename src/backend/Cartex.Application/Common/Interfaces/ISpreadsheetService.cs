namespace Cartex.Application.Common.Interfaces;

public interface ISpreadsheetService
{
    IReadOnlyList<IReadOnlyList<string>> Read(Stream content);
    byte[] Write(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows);
}
