using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Users.Queries;

public record GetUsersQuery : FilteringRequest, IRequest<IReadOnlyCollection<UserDto>>;

public record UserDto(long Id, string FullName, string Username, string RoleName, string? DefaultBranchName, bool IsActive);

public sealed class GetUsersQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetUsersQuery, IReadOnlyCollection<UserDto>>
{
    public async Task<IReadOnlyCollection<UserDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        return await db.Users
            .Include(u => u.Role)
            .Include(u => u.DefaultBranch)
            .ToPagedListAsync(request,
                u => new UserDto(u.Id, u.FullName, u.Username, u.Role.Name, u.DefaultBranch != null ? u.DefaultBranch.Name : null, u.IsActive),
                writer, cancellationToken);
    }
}
