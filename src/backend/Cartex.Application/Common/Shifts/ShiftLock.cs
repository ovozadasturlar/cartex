using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;

namespace Cartex.Application.Common.Shifts;

public interface IShiftLock
{
    Task<Shift?> OpenAsync(long userId, long branchId, CancellationToken cancellationToken);
    Task<Shift?> ByIdAsync(long shiftId, CancellationToken cancellationToken);
}

public sealed class ShiftLock(IApplicationDbContext db) : IShiftLock
{
    public async Task<Shift?> OpenAsync(long userId, long branchId, CancellationToken cancellationToken) =>
        (await db.LockAsync<Shift>(
            $"SELECT * FROM shifts WHERE user_id = {userId} AND branch_id = {branchId} AND status = {nameof(ShiftStatus.Open)} AND is_deleted = false ORDER BY id FOR UPDATE",
            cancellationToken)).FirstOrDefault();

    public async Task<Shift?> ByIdAsync(long shiftId, CancellationToken cancellationToken) =>
        (await db.LockAsync<Shift>(
            $"SELECT * FROM shifts WHERE id = {shiftId} AND is_deleted = false FOR UPDATE",
            cancellationToken)).FirstOrDefault();
}
