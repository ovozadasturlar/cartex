using Cartex.Application.Categories;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Categories.Commands;

public record MergeCategoryCommand(long SourceId, long TargetId) : ICommand<int>;

public sealed class MergeCategoryCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditService audit) : IRequestHandler<MergeCategoryCommand, int>
{
    public async Task<int> Handle(MergeCategoryCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.Categories.Edit))
            throw new ForbiddenException("Kategoriyalarni birlashtirishga ruxsat yo'q.");
        if (request.SourceId == request.TargetId)
            throw new BusinessRuleException("Kategoriya o'zi bilan birlashtirilmaydi.", "category_merge_same");

        var categories = await db.Categories.ToListAsync(cancellationToken);
        var source = categories.FirstOrDefault(x => x.Id == request.SourceId)
            ?? throw new NotFoundException("Manba kategoriya topilmadi.", "category_not_found");
        var target = categories.FirstOrDefault(x => x.Id == request.TargetId)
            ?? throw new NotFoundException("Maqsad kategoriya topilmadi.", "category_not_found");
        if (CategoryTreeRules.IsDescendant(categories, source.Id, target.Id))
            throw new BusinessRuleException("Kategoriyani o'z avlodiga birlashtirib bo'lmaydi.", "category_cycle");

        var sourceChildren = categories
            .Where(x => x.ParentId == source.Id)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToList();
        foreach (var child in sourceChildren)
            CategoryTreeRules.ValidateParent(categories, child, target.Id);

        var targetChildren = categories
            .Where(x => x.Id != source.Id && x.ParentId == target.Id)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToList();
        foreach (var child in sourceChildren)
        {
            child.ParentId = target.Id;
            child.SortOrder = targetChildren.Count;
            targetChildren.Add(child);
        }
        CategoryTreeRules.Normalize(targetChildren);
        if (source.ParentId != target.Id)
            CategoryTreeRules.Normalize(categories.Where(x => x.Id != source.Id && x.ParentId == source.ParentId)
                .OrderBy(x => x.SortOrder).ThenBy(x => x.Id));

        var productCount = await db.Products.CountAsync(x => x.CategoryId == source.Id, cancellationToken);
        await db.Products
            .Where(x => x.CategoryId == source.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.CategoryId, target.Id)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow), cancellationToken);
        source.IsDeleted = true;
        source.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        audit.SetOutcome("category.merged", "categories", source.Id, new
        {
            sourceId = source.Id,
            targetId = target.Id,
            movedProductCount = productCount,
            childCount = sourceChildren.Count
        }, "Kategoriyalar birlashtirildi");
        return productCount;
    }
}

public sealed class MergeCategoryCommandValidator : AbstractValidator<MergeCategoryCommand>
{
    public MergeCategoryCommandValidator()
    {
        RuleFor(x => x.SourceId).GreaterThan(0);
        RuleFor(x => x.TargetId).GreaterThan(0);
        RuleFor(x => x).Must(x => x.SourceId != x.TargetId).WithMessage("Kategoriyalar har xil bo'lishi kerak.");
    }
}
