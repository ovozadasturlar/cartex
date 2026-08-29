using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Cartex.Shared.Models.Shifts;
using Microsoft.EntityFrameworkCore;

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

        var baseCode = await db.Businesses.Select(b => b.Currency).FirstAsync(cancellationToken);
        var query = db.Shifts.Where(s => s.BranchId == branchId);
        if (!currentUser.HasPermission(AppPermissions.Shifts.ViewAll))
            query = query.Where(s => s.UserId == currentUser.UserId);
        else if (request.UserId is { } uid)
            query = query.Where(s => s.UserId == uid);

        return await query
            .ToPagedListAsync(request,
                s => new ShiftHistoryDto(s.Id, s.UserId, s.User.FullName, s.OpenedAt, s.ClosedAt,
                    s.CashRows.Where(c => c.Currency == baseCode).Select(c => c.OpeningFloat).FirstOrDefault(),
                    s.CashRows.Where(c => c.Currency == baseCode).Select(c => c.CountedCash).FirstOrDefault(),
                    s.Status.ToString()),
                writer, cancellationToken);
    }
}
