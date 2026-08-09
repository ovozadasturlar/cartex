using Cartex.Persistence;

namespace Cartex.Application.Common.Documents;

public static class DocumentNumbers
{
    public static async Task<string> NextAsync(
        IApplicationDbContext db,
        string prefix,
        DateOnly businessDate,
        CancellationToken cancellationToken)
    {
        var sequence = await db.NextDocumentSequenceAsync(cancellationToken);
        return $"{prefix}-{businessDate:yyyyMMdd}-{sequence:D8}";
    }
}
