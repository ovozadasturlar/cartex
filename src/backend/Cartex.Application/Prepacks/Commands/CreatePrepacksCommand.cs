using System.Security.Cryptography;
using Cartex.Application.Common.Finance;
using Cartex.Application.Common.Messaging;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Prepacks.Commands;

public record PrepackLabelDto(long Id, string LabelCode, string ProductName, string UnitName, decimal Quantity, decimal Price);

public record CreatePrepacksCommand(long WarehouseId, long VariantId, decimal Quantity, int Count = 1, int? ExpiresHours = null) : ICommand<List<PrepackLabelDto>>;

public sealed class CreatePrepacksCommandHandler(IApplicationDbContext db, ICurrencyService currency) : IRequestHandler<CreatePrepacksCommand, List<PrepackLabelDto>>
{
    public async Task<List<PrepackLabelDto>> Handle(CreatePrepacksCommand request, CancellationToken cancellationToken)
    {
        var warehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken)
            ?? throw new NotFoundException("Warehouse not found.");

        var variant = await db.ProductVariants
            .Where(v => v.Id == request.VariantId)
            .Select(v => new { v.Id, v.Product.Name, UnitName = v.Product.Unit.ShortName })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Mahsulot topilmadi.");

        var baseCode = await currency.BaseAsync(cancellationToken);
        var price = await db.ProductPrices
            .Where(p => p.VariantId == request.VariantId && (p.WarehouseId == request.WarehouseId || p.WarehouseId == null))
            .OrderBy(p => p.WarehouseId == null ? 1 : 0)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleException("Mahsulot narxi belgilanmagan.");
        var rate = price.Currency == baseCode ? 1m : await currency.RateAsync(price.Currency, cancellationToken);
        var unitPrice = Math.Round(price.SellingPrice * rate, 2);

        var now = DateTime.UtcNow;
        var expiresAt = request.ExpiresHours is { } hours ? now.AddHours(hours) : (DateTime?)null;

        var result = new List<PrepackLabelDto>();
        for (var i = 0; i < request.Count; i++)
        {
            var prepack = new Prepack
            {
                BranchId = warehouse.BranchId,
                WarehouseId = warehouse.Id,
                VariantId = variant.Id,
                Quantity = request.Quantity,
                UnitPrice = unitPrice,
                LabelCode = $"PP{RandomNumberGenerator.GetInt32(0, 1_000_000_000):D9}{RandomNumberGenerator.GetInt32(0, 10)}",
                Status = PrepackStatus.Active,
                ExpiresAt = expiresAt
            };
            db.Prepacks.Add(prepack);
            result.Add(new PrepackLabelDto(0, prepack.LabelCode, variant.Name, variant.UnitName, prepack.Quantity,
                Math.Round(prepack.UnitPrice * prepack.Quantity, 2)));
        }

        await db.SaveChangesAsync(cancellationToken);
        return result;
    }
}

public sealed class CreatePrepacksCommandValidator : AbstractValidator<CreatePrepacksCommand>
{
    public CreatePrepacksCommandValidator()
    {
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Count).InclusiveBetween(1, 100);
        RuleFor(x => x.ExpiresHours).GreaterThan(0).When(x => x.ExpiresHours.HasValue);
    }
}
