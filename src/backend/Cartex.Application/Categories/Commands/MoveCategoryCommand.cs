using Cartex.Application.Categories;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Categories.Commands;

public record MoveCategoryCommand(long Id, long? ParentId, int SortOrder) : ICommand<Unit>;

public sealed class MoveCategoryCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditService audit) : IRequestHandler<MoveCategoryCommand, Unit>
{
    public async Task<Unit> Handle(MoveCategoryCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.Categories.Edit))
            throw new ForbiddenException("Kategoriyalar tartibini o'zgartirishga ruxsat yo'q.");

        var categories = await db.Categories.ToListAsync(cancellationToken);
        var category = categories.FirstOrDefault(x => x.Id == request.Id)
            ?? throw new NotFoundException("Kategoriya topilmadi.", "category_not_found");
        var oldParentId = category.ParentId;
        var oldSortOrder = category.SortOrder;
        CategoryTreeRules.Move(categories, category, request.ParentId, request.SortOrder);
        await db.SaveChangesAsync(cancellationToken);
        audit.SetOutcome(oldParentId == category.ParentId ? "category.reordered" : "category.reparented",
            "categories", category.Id, new
            {
                oldParentId,
                newParentId = category.ParentId,
                oldSortOrder,
                newSortOrder = category.SortOrder
            }, oldParentId == category.ParentId
                ? "Kategoriya tartibi o'zgartirildi"
                : "Kategoriya boshqa shoxga ko'chirildi");
        return Unit.Value;
    }
}

public sealed class MoveCategoryCommandValidator : AbstractValidator<MoveCategoryCommand>
{
    public MoveCategoryCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}
