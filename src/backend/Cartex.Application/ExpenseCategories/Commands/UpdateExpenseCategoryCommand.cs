using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;

namespace Cartex.Application.ExpenseCategories.Commands;

public record UpdateExpenseCategoryCommand(long Id, string Name) : ICommand<Unit>;

public sealed class UpdateExpenseCategoryCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateExpenseCategoryCommand, Unit>
{
    public async Task<Unit> Handle(UpdateExpenseCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await db.ExpenseCategories.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Expense category not found.");

        category.Name = request.Name;
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class UpdateExpenseCategoryCommandValidator : AbstractValidator<UpdateExpenseCategoryCommand>
{
    public UpdateExpenseCategoryCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}
