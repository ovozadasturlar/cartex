using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;

namespace Cartex.Application.Categories.Commands;

public record UpdateCategoryCommand(long Id, string Name, long? ParentId, string? Description = null) : ICommand<Unit>;

public sealed class UpdateCategoryCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateCategoryCommand, Unit>
{
    public async Task<Unit> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Category not found.");

        category.Name = request.Name;
        category.ParentId = request.ParentId;
        category.Description = request.Description;

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class UpdateCategoryCommandValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x).Must(x => x.ParentId != x.Id).WithMessage("Category cannot be its own parent.");
    }
}
