using Cartex.Application.Common.Loyalty;
using Cartex.Shared.Models.Loyalty;

namespace Cartex.Application.Loyalty.Queries;

public record PreviewDiscountItem(long VariantId, decimal Quantity, decimal UnitPrice);

public record PreviewDiscountResult(decimal Total, List<DiscountApplicationDto> Applied);

public record PreviewDiscountQuery(long? CustomerId, List<PreviewDiscountItem> Items) : IRequest<PreviewDiscountResult>;

public sealed class PreviewDiscountQueryHandler(IDiscountCalculator calculator) : IRequestHandler<PreviewDiscountQuery, PreviewDiscountResult>
{
    public async Task<PreviewDiscountResult> Handle(PreviewDiscountQuery request, CancellationToken cancellationToken)
    {
        var lines = request.Items
            .Select(i => new DiscountCalcLine(i.VariantId, i.Quantity * i.UnitPrice))
            .ToList();
        var applied = await calculator.CalculateAsync(request.CustomerId, lines, cancellationToken);
        return new PreviewDiscountResult(applied.Sum(a => a.Amount),
            [.. applied.Select(a => new DiscountApplicationDto(a.Name, a.Amount))]);
    }
}
