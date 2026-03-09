using MediatR;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;

namespace Cartex.Application.Suppliers.Queries;

public record GetSuppliersQuery : IRequest<List<SupplierDto>>;

public record SupplierDto(long Id, string Name, string? Phone);

public sealed class GetSuppliersQueryHandler(IApplicationDbContext db) : IRequestHandler<GetSuppliersQuery, List<SupplierDto>>
{
    public async Task<List<SupplierDto>> Handle(GetSuppliersQuery request, CancellationToken cancellationToken)
    {
        return await db.Suppliers
            .Select(s => new SupplierDto(s.Id, s.Name, s.Phone))
            .ToListAsync(cancellationToken);
    }
}
