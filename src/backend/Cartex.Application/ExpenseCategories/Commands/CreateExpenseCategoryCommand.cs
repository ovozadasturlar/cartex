using Cartex.Application.Common.Messaging;
using FluentValidation;
using Cartex.Persistence;
using Cartex.Domain.Entities;

namespace Cartex.Application.ExpenseCategories.Commands;

public record CreateExpenseCategoryCommand(string Name) : ICommand<long>;

public sealed class CreateExpenseCategoryCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateExpenseCategoryCommand, long>
{
    public async Task<long> Handle(CreateExpenseCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = new ExpenseCategory { Name = request.Name };
        db.ExpenseCategories.Add(category);
        await db.SaveChangesAsync(cancellationToken);
        return category.Id;
    }
}

public sealed class CreateExpenseCategoryCommandValidator : AbstractValidator<CreateExpenseCategoryCommand>
{
    public CreateExpenseCategoryCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}
