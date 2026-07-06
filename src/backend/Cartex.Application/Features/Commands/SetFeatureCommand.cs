using Cartex.Application.Common.Messaging;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Features.Commands;

public record SetFeatureCommand(string Code, bool IsEnabled) : ICommand<Unit>;

public sealed class SetFeatureCommandHandler(IApplicationDbContext db, IFeatureStateProvider features)
    : IRequestHandler<SetFeatureCommand, Unit>
{
    public async Task<Unit> Handle(SetFeatureCommand request, CancellationToken cancellationToken)
    {
        var feature = await db.Features.FirstOrDefaultAsync(f => f.Code == request.Code, cancellationToken)
            ?? throw new NotFoundException("Feature not found.");

        feature.IsEnabled = request.IsEnabled;
        await db.SaveChangesAsync(cancellationToken);
        features.Invalidate();
        return Unit.Value;
    }
}
