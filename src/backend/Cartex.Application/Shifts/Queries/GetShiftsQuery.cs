using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Cartex.Shared.Models.Shifts;

namespace Cartex.Application.Shifts.Queries;

public record GetShiftsQuery : FilteringRequest, IRequest<IReadOnlyCollection<ShiftHistoryDto>>
{
    public long? UserId { get; set; }
}

public sealed class GetShiftsQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IPagingMetadataWriter writer) : IRequestHandler<GetShiftsQuery, IReadOnlyCollection<ShiftHistoryDto>>
{
    public async Task<IReadOnlyCollection<ShiftHistoryDto>> Handle(GetShiftsQuery request, CancellationToken cancellationToken)
    {
        var branchId = currentUser.DefaultBranchId;
        if (branchId is null)
            return [];

        if (string.IsNullOrWhiteSpace(request.SortBy))
        {
            request.SortBy = nameof(Domain.Entities.Shift.OpenedAt);
            request.Descending = true;
        }

        var query = db.Shifts.Where(s => s.BranchId == branchId);
        if (!currentUser.HasPermission(AppPermissions.Shifts.ViewAll))
            query = query.Where(s => s.UserId == currentUser.UserId);
        else if (request.UserId is { } uid)
            query = query.Where(s => s.UserId == uid);

        return await query
            .ToPagedListAsync(request,
                s => new ShiftHistoryDto(s.Id, s.UserId, s.User.FullName, s.OpenedAt, s.ClosedAt, s.OpeningFloat, s.CountedCash, s.Status.ToString()),
                writer, cancellationToken);
    }
}
