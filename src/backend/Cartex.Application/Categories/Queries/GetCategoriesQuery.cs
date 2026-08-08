using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Categories.Queries;

public record GetCategoriesQuery : FilteringRequest, IRequest<IReadOnlyCollection<CategoryDto>>;

public record CategoryDto(long Id, string Name, string? Description, long? ParentId, string? ParentName);

public sealed class GetCategoriesQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetCategoriesQuery, IReadOnlyCollection<CategoryDto>>
{
    public async Task<IReadOnlyCollection<CategoryDto>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SortBy))
        {
            request.SortBy = "Id";
            request.Descending = false;
        }
        return await db.Categories
            .Include(c => c.Parent)
            .ToPagedListAsync(request,
                c => new CategoryDto(
                    c.Id,
                    c.Name,
                    c.Description,
                    c.ParentId,
                    c.Parent != null ? c.Parent.Name : null),
                writer, cancellationToken);
    }
}
