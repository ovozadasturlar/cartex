using Cartex.Application.Common.Interfaces;

namespace Cartex.Application.Products.Import;

public record GetImportTemplateQuery : IRequest<byte[]>;

public sealed class GetImportTemplateQueryHandler(ISpreadsheetService spreadsheet) : IRequestHandler<GetImportTemplateQuery, byte[]>
{
    private static readonly string[] Headers =
    [
        "Nomi", "Barkod", "Pachka soni", "Artikul", "Kategoriya", "Birlik",
        "Sotish narxi", "Kirim narxi", "Soni", "Yaroqlilik muddati", "Min qoldiq"
    ];

    public Task<byte[]> Handle(GetImportTemplateQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(spreadsheet.Write(Headers, []));
}
