using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Application.Categories;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;

namespace Cartex.Application.Categories.Commands;

public record UpdateCategoryCommand(long Id, string Name, long? ParentId, string? Description = null) : ICommand<Unit>;

public sealed class UpdateCategoryCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditService audit) : IRequestHandler<UpdateCategoryCommand, Unit>
{
    public async Task<Unit> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var categories = await db.Categories.ToListAsync(cancellationToken);
        var category = categories.FirstOrDefault(c => c.Id == request.Id)
            ?? throw new NotFoundException("Category not found.");

        if (category.ParentId != request.ParentId)
        {
            if (!currentUser.HasPermission(AppPermissions.Categories.Edit))
                throw new ForbiddenException("Kategoriyani boshqa ota-onaga ko'chirishga ruxsat yo'q.");
            var oldParentId = category.ParentId;
            var targetIndex = categories.Count(x => x.Id != category.Id && x.ParentId == request.ParentId);
            CategoryTreeRules.Move(categories, category, request.ParentId, targetIndex);
            audit.SetOutcome("category.reparented", "categories", category.Id, new
            {
                oldParentId,
                newParentId = request.ParentId,
                category.SortOrder
            }, "Kategoriya boshqa shoxga ko'chirildi");
        }

        category.Name = request.Name;
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
