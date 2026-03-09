using MediatR;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;

namespace Cartex.Application.Categories.Queries;

public record GetCategoriesQuery : IRequest<List<CategoryDto>>;

public record CategoryDto(long Id, string Name, long? ParentId, string? ParentName);

public sealed class GetCategoriesQueryHandler(IApplicationDbContext db) : IRequestHandler<GetCategoriesQuery, List<CategoryDto>>
{
    public async Task<List<CategoryDto>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        return await db.Categories
            .Include(c => c.Parent)
            .Select(c => new CategoryDto(
                c.Id,
                c.Name,
                c.ParentId,
                c.Parent != null ? c.Parent.Name : null))
            .ToListAsync(cancellationToken);
    }
}
