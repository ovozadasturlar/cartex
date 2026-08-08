using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Persistence;

namespace Cartex.Application.Rates.Commands;

public record SetExchangeRateCommand(string Code, decimal Rate) : ICommand<long>;

public sealed class SetExchangeRateCommandHandler(IApplicationDbContext db, ICurrentUser currentUser) : IRequestHandler<SetExchangeRateCommand, long>
{
    public async Task<long> Handle(SetExchangeRateCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");

        var code = request.Code.Trim().ToUpperInvariant();
        var baseCurrency = await db.Businesses.Select(b => b.Currency).FirstAsync(cancellationToken);
        if (code == baseCurrency)
            throw new BusinessRuleException("Bazaviy valyuta uchun kurs kiritilmaydi.");

        var rate = new ExchangeRate { Code = code, Rate = request.Rate, UserId = userId };
        db.ExchangeRates.Add(rate);
        await db.SaveChangesAsync(cancellationToken);

        return rate.Id;
    }
}

public sealed class SetExchangeRateCommandValidator : AbstractValidator<SetExchangeRateCommand>
{
    public SetExchangeRateCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Length(3);
        RuleFor(x => x.Rate).GreaterThan(0);
    }
}
