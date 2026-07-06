using Cartex.Domain.Common;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Tests.Common;

public sealed class TestFeatureStates(ApplicationDbContext db) : IFeatureStateProvider
{
    public async Task<bool> IsEnabledAsync(string code, CancellationToken cancellationToken = default) =>
        await db.Features.Where(f => f.Code == code).Select(f => f.IsEnabled).FirstOrDefaultAsync(cancellationToken);

    public void Invalidate() { }
}
