using MediatR;
using FluentValidation;
using Cartex.Persistence;
using Cartex.Domain.Entities;

namespace Cartex.Application.Shops.Commands;

public record CreateShopCommand(string Name, string? Address, string? Phone, decimal CashbackRate) : IRequest<long>;

public sealed class CreateShopCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateShopCommand, long>
{
    public async Task<long> Handle(CreateShopCommand request, CancellationToken cancellationToken)
    {
        var shop = new Shop
        {
            Name = request.Name,
            Address = request.Address,
            Phone = request.Phone,
            CashbackRate = request.CashbackRate
        };

        db.Shops.Add(shop);
        await db.SaveChangesAsync(cancellationToken);

        return shop.Id;
    }
}

public sealed class CreateShopCommandValidator : AbstractValidator<CreateShopCommand>
{
    public CreateShopCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}
