using Cartex.Application.Common.Interfaces;

namespace Cartex.Application.Supplies.Import;

public record GetSupplyImportTemplateQuery : IRequest<byte[]>;

public sealed class GetSupplyImportTemplateQueryHandler(ISpreadsheetService spreadsheet)
    : IRequestHandler<GetSupplyImportTemplateQuery, byte[]>
{
    private static readonly string[] Headers =
    [
        "Nomi", "Barkod", "Artikul", "Soni", "Kirim narxi", "Sotish narxi", "Yaroqlilik muddati"
    ];

    public Task<byte[]> Handle(GetSupplyImportTemplateQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(spreadsheet.Write(Headers, []));
}
