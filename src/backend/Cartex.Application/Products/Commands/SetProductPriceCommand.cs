using Cartex.Application.Common.Finance;
using FluentValidation;
using Cartex.Persistence;

namespace Cartex.Application.Products.Commands;

public record SetProductPriceCommand(long VariantId, long? WarehouseId, decimal SellingPrice, string? Currency = null) : ICommand<Unit>;

public sealed class SetProductPriceCommandHandler(IApplicationDbContext db, ICurrencyService currency, IAuditService audit) : IRequestHandler<SetProductPriceCommand, Unit>
{
    public async Task<Unit> Handle(SetProductPriceCommand request, CancellationToken cancellationToken)
    {
        await currency.EnsurePricingAllowedAsync(request.Currency, cancellationToken);
        await ProductPriceWriter.UpsertAsync(db, request.VariantId, request.WarehouseId, request.SellingPrice, cancellationToken, request.Currency);
        audit.Add("setPrice", "product_prices", request.VariantId, new { request.VariantId, request.WarehouseId, request.SellingPrice, request.Currency });
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class SetProductPriceCommandValidator : AbstractValidator<SetProductPriceCommand>
{
    public SetProductPriceCommandValidator()
    {
        RuleFor(x => x.VariantId).GreaterThan(0);
        RuleFor(x => x.SellingPrice).GreaterThanOrEqualTo(0);
    }
}
