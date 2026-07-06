using Cartex.Domain.Common;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Shifts.Queries;

public record GetShiftReportQuery(long ShiftId) : IRequest<ZReportDto>;

public sealed class GetShiftReportQueryHandler(IApplicationDbContext db, ICurrentUser currentUser) : IRequestHandler<GetShiftReportQuery, ZReportDto>
{
    public async Task<ZReportDto> Handle(GetShiftReportQuery request, CancellationToken cancellationToken)
    {
        var shift = await db.Shifts.FirstOrDefaultAsync(
            s => s.Id == request.ShiftId && s.BranchId == currentUser.DefaultBranchId, cancellationToken)
            ?? throw new NotFoundException("Shift not found.");

        return await ShiftCalculator.ComputeAsync(db, shift, shift.CountedCash ?? 0, cancellationToken);
    }
}
