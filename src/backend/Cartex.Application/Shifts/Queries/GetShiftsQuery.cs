using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;

namespace Cartex.Application.Shifts.Queries;

public record GetShiftsQuery : FilteringRequest, IRequest<IReadOnlyCollection<ShiftHistoryDto>>;

public record ShiftHistoryDto(long Id, string UserName, DateTime OpenedAt, DateTime? ClosedAt, decimal OpeningFloat, decimal? CountedCash, string Status);

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

        return await db.Shifts
            .Where(s => s.BranchId == branchId)
            .OrderByDescending(s => s.OpenedAt)
            .ToPagedListAsync(request,
                s => new ShiftHistoryDto(s.Id, s.User.FullName, s.OpenedAt, s.ClosedAt, s.OpeningFloat, s.CountedCash, s.Status.ToString()),
                writer, cancellationToken);
    }
}
