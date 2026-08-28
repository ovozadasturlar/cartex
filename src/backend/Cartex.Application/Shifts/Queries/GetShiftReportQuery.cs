using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.Shifts;

namespace Cartex.Application.Shifts.Queries;

public record GetShiftReportQuery(long ShiftId) : IRequest<ZReportDto>;

public sealed class GetShiftReportQueryHandler(IApplicationDbContext db, ICurrentUser currentUser) : IRequestHandler<GetShiftReportQuery, ZReportDto>
{
    public async Task<ZReportDto> Handle(GetShiftReportQuery request, CancellationToken cancellationToken)
    {
        var shift = await db.Shifts.FirstOrDefaultAsync(
            s => s.Id == request.ShiftId && s.BranchId == currentUser.DefaultBranchId, cancellationToken)
            ?? throw new NotFoundException("Shift not found.");

        if (shift.UserId != currentUser.UserId && !currentUser.HasPermission(AppPermissions.Shifts.ViewAll))
            throw new ForbiddenException("Boshqa kassir smenasini ko'rishga ruxsat yo'q.");

        var baseCode = await db.Businesses.Select(b => b.Currency).FirstAsync(cancellationToken);
        var counted = await db.ShiftCashes
            .Where(c => c.ShiftId == shift.Id && c.Currency == baseCode)
            .Select(c => c.CountedCash)
            .FirstOrDefaultAsync(cancellationToken);
        return await ShiftCalculator.ComputeAsync(db, shift, counted ?? 0, cancellationToken);
    }
}
