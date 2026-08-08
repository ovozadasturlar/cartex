using FluentValidation;
using Cartex.Persistence;
using Cartex.Domain.Entities;

namespace Cartex.Application.Categories.Commands;

public record CreateCategoryCommand(string Name, long? ParentId, string? Description = null) : ICommand<long>;

public sealed class CreateCategoryCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateCategoryCommand, long>
{
    public async Task<long> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = new Category
        {
            Name = request.Name,
            ParentId = request.ParentId,
            Description = request.Description
        };

        db.Categories.Add(category);
        await db.SaveChangesAsync(cancellationToken);

        return category.Id;
    }
}

public sealed class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}
