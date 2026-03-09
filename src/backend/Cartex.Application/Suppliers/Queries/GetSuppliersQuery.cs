using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using MediatR;

namespace Cartex.Application.Suppliers.Queries;

public record GetSuppliersQuery : FilteringRequest, IRequest<IReadOnlyCollection<SupplierDto>>;

public record SupplierDto(long Id, string Name, string? Phone);

public sealed class GetSuppliersQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetSuppliersQuery, IReadOnlyCollection<SupplierDto>>
{
    public async Task<IReadOnlyCollection<SupplierDto>> Handle(GetSuppliersQuery request, CancellationToken cancellationToken)
    {
        return await db.Suppliers
            .ToPagedListAsync(request,
                s => new SupplierDto(s.Id, s.Name, s.Phone),
                writer, cancellationToken);
    }
}
