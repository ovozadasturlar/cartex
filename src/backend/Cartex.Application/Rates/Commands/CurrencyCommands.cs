using Cartex.Application.Common.Messaging;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using FluentValidation;
using Unit = Cartex.Application.Common.Messaging.Unit;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Rates.Commands;

public record CreateCurrencyCommand(string Code, string Name) : ICommand<long>;

public sealed class CreateCurrencyCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateCurrencyCommand, long>
{
    public async Task<long> Handle(CreateCurrencyCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.Currencies.AnyAsync(c => c.Code == code, cancellationToken))
            throw new BusinessRuleException("Bu valyuta allaqachon mavjud.");

        var currency = new Currency { Code = code, Name = request.Name.Trim() };
        db.Currencies.Add(currency);
        await db.SaveChangesAsync(cancellationToken);
        return currency.Id;
    }
}

public sealed class CreateCurrencyCommandValidator : AbstractValidator<CreateCurrencyCommand>
{
    public CreateCurrencyCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Length(3).Matches("^[A-Za-z]{3}$");
        RuleFor(x => x.Name).MaximumLength(40);
    }
}

public record UpdateCurrencyCommand(string Code, bool IsEnabled, bool IsDefault) : ICommand<Unit>;

public sealed class UpdateCurrencyCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateCurrencyCommand, Unit>
{
    public async Task<Unit> Handle(UpdateCurrencyCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var currency = await db.Currencies.FirstOrDefaultAsync(c => c.Code == code, cancellationToken)
            ?? throw new NotFoundException("Currency not found.");

        var baseCurrency = await db.Businesses.Select(b => b.Currency).FirstAsync(cancellationToken);
        if (code == baseCurrency && !request.IsEnabled)
            throw new BusinessRuleException("Bazaviy valyutani o'chirib bo'lmaydi.");

        currency.IsEnabled = request.IsEnabled;

        if (request.IsDefault && !currency.IsDefault)
        {
            if (!request.IsEnabled)
                throw new BusinessRuleException("O'chirilgan valyuta standart bo'la olmaydi.");
            await db.Currencies
                .Where(c => c.IsDefault && c.Id != currency.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDefault, false), cancellationToken);
        }
        currency.IsDefault = request.IsDefault && request.IsEnabled;

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public record DeleteCurrencyCommand(string Code) : ICommand<Unit>;

public sealed class DeleteCurrencyCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteCurrencyCommand, Unit>
{
    public async Task<Unit> Handle(DeleteCurrencyCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var currency = await db.Currencies.FirstOrDefaultAsync(c => c.Code == code, cancellationToken)
            ?? throw new NotFoundException("Currency not found.");

        if (currency.IsSystem)
            throw new BusinessRuleException("Tizim valyutasini o'chirib tashlab bo'lmaydi — faqat nofaol qilish mumkin.");

        if (await db.Accounts.AnyAsync(a => a.Currency == code, cancellationToken))
            throw new BusinessRuleException("Bu valyutada hisoblar bor — o'chirib bo'lmaydi, nofaol qiling.");

        await db.ExchangeRates.Where(r => r.Code == code).ExecuteDeleteAsync(cancellationToken);
        db.Currencies.Remove(currency);
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
