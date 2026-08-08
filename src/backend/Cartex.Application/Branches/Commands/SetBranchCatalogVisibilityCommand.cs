using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Domain.Common;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.Branches.Commands;

public record SetBranchCatalogVisibilityCommand(long BranchId, long VariantId, string VisibilityOverride) : ICommand<Unit>;

public sealed class SetBranchCatalogVisibilityCommandHandler(IApplicationDbContext db, ICurrentUser currentUser) : IRequestHandler<SetBranchCatalogVisibilityCommand, Unit>
{
    public async Task<Unit> Handle(SetBranchCatalogVisibilityCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.CanAccessAllBranches && !currentUser.BranchIds.Contains(request.BranchId))
            throw new ForbiddenException("Branch access denied.");

        if (!Enum.TryParse<BranchCatalogVisibilityOverride>(request.VisibilityOverride, true, out var visibility))
            throw new BusinessRuleException("Invalid catalog visibility.");

        if (!await db.Branches.AnyAsync(x => x.Id == request.BranchId, cancellationToken))
            throw new NotFoundException("Branch not found.");
        if (!await db.ProductVariants.AnyAsync(x => x.Id == request.VariantId, cancellationToken))
            throw new NotFoundException("Product variant not found.");

        var entry = await db.BranchCatalogEntries
            .FirstOrDefaultAsync(x => x.BranchId == request.BranchId && x.VariantId == request.VariantId, cancellationToken);

        if (entry is null)
        {
            entry = new BranchCatalogEntry
            {
                BranchId = request.BranchId,
                VariantId = request.VariantId,
                ActivationSource = BranchCatalogActivationSource.Manual,
                VisibilityOverride = visibility
            };
            db.BranchCatalogEntries.Add(entry);
        }
        else
            entry.VisibilityOverride = visibility;

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class SetBranchCatalogVisibilityCommandValidator : AbstractValidator<SetBranchCatalogVisibilityCommand>
{
    public SetBranchCatalogVisibilityCommandValidator()
    {
        RuleFor(x => x.BranchId).GreaterThan(0);
        RuleFor(x => x.VariantId).GreaterThan(0);
        RuleFor(x => x.VisibilityOverride).NotEmpty().MaximumLength(20);
    }
}
