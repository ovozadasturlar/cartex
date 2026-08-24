using Cartex.Application.Common.Messaging;
using Cartex.Shared.Models.Stocks;
using FluentValidation;

namespace Cartex.Application.Stocks.Queries;

public sealed record GetStockOnHandByVariantsQuery(long WarehouseId, IReadOnlyList<long> VariantIds)
    : IRequest<IReadOnlyList<StockOnHandDto>>;

public sealed class GetStockOnHandByVariantsQueryValidator : AbstractValidator<GetStockOnHandByVariantsQuery>
{
    public GetStockOnHandByVariantsQueryValidator()
    {
        RuleFor(x => x.WarehouseId).GreaterThan(0);
        RuleFor(x => x.VariantIds).NotEmpty().Must(x => x is { Count: <= 500 });
        RuleForEach(x => x.VariantIds).GreaterThan(0);
    }
}

public sealed class GetStockOnHandByVariantsQueryHandler(ISender sender)
    : IRequestHandler<GetStockOnHandByVariantsQuery, IReadOnlyList<StockOnHandDto>>
{
    public async Task<IReadOnlyList<StockOnHandDto>> Handle(
        GetStockOnHandByVariantsQuery request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetStockOnHandQuery(
            request.WarehouseId,
            Page: 0,
            PageSize: 0,
            VariantIds: request.VariantIds), cancellationToken);
        return result.Items;
    }
}
