using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.ExpenseCategories.Queries;

public record GetExpenseCategoriesQuery : IRequest<IReadOnlyCollection<ExpenseCategoryDto>>;

public record ExpenseCategoryDto(long Id, string Name);

public sealed class GetExpenseCategoriesQueryHandler(IApplicationDbContext db) : IRequestHandler<GetExpenseCategoriesQuery, IReadOnlyCollection<ExpenseCategoryDto>>
{
    public async Task<IReadOnlyCollection<ExpenseCategoryDto>> Handle(GetExpenseCategoriesQuery request, CancellationToken cancellationToken) =>
        await db.ExpenseCategories
            .OrderBy(c => c.Name)
            .Select(c => new ExpenseCategoryDto(c.Id, c.Name))
            .ToListAsync(cancellationToken);
}
