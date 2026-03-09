using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Categories.Queries;

public record GetCategoriesQuery : FilteringRequest, IRequest<IReadOnlyCollection<CategoryDto>>;

public record CategoryDto(long Id, string Name, long? ParentId, string? ParentName);

public sealed class GetCategoriesQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetCategoriesQuery, IReadOnlyCollection<CategoryDto>>
{
    public async Task<IReadOnlyCollection<CategoryDto>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        return await db.Categories
            .Include(c => c.Parent)
            .ToPagedListAsync(request,
                c => new CategoryDto(
                    c.Id,
                    c.Name,
                    c.ParentId,
                    c.Parent != null ? c.Parent.Name : null),
                writer, cancellationToken);
    }
}
