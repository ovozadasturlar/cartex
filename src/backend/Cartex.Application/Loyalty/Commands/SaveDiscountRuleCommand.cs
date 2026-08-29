using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Loyalty.Commands;

public record DiscountExceptionInput(DiscountScope Scope, long TargetId);

public record SaveDiscountRuleCommand(long Id, string Name, bool IsEnabled, DiscountScope Scope, long? TargetId,
    long? CustomerId, decimal MinAmount, DiscountMethod Method, decimal Value, int Priority,
    DateOnly? StartsOn, DateOnly? EndsOn, List<DiscountExceptionInput>? Exceptions = null) : ICommand<long>;

public sealed class SaveDiscountRuleCommandHandler(IApplicationDbContext db) : IRequestHandler<SaveDiscountRuleCommand, long>
{
    public async Task<long> Handle(SaveDiscountRuleCommand request, CancellationToken cancellationToken)
    {
        DiscountRule rule;
        if (request.Id == 0)
        {
            rule = new DiscountRule();
            db.DiscountRules.Add(rule);
        }
        else
        {
            rule = await db.DiscountRules.Include(r => r.Exceptions).FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
                ?? throw new NotFoundException("Discount rule not found.");
        }

        rule.Name = request.Name.Trim();
        rule.IsEnabled = request.IsEnabled;
        rule.Scope = request.Scope;
        rule.TargetId = request.Scope == DiscountScope.All ? null : request.TargetId;
        rule.CustomerId = request.CustomerId;
        rule.MinAmount = request.MinAmount;
        rule.Method = request.Method;
        rule.Value = request.Value;
        rule.Priority = request.Priority;
        rule.StartsOn = request.StartsOn;
        rule.EndsOn = request.EndsOn;

        var wanted = (request.Exceptions ?? []).Distinct().ToHashSet();
        foreach (var stale in rule.Exceptions.Where(e => !wanted.Contains(new DiscountExceptionInput(e.Scope, e.TargetId))).ToList())
            rule.Exceptions.Remove(stale);
        foreach (var entry in wanted.Where(w => rule.Exceptions.All(e => e.Scope != w.Scope || e.TargetId != w.TargetId)))
            rule.Exceptions.Add(new DiscountRuleException { Scope = entry.Scope, TargetId = entry.TargetId });

        await db.SaveChangesAsync(cancellationToken);
        return rule.Id;
    }
}

public sealed class SaveDiscountRuleCommandValidator : AbstractValidator<SaveDiscountRuleCommand>
{
    public SaveDiscountRuleCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Value).GreaterThan(0);
        RuleFor(x => x.Value).LessThanOrEqualTo(100).When(x => x.Method == DiscountMethod.Percent);
        RuleFor(x => x.MinAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TargetId).NotNull().When(x => x.Scope != DiscountScope.All)
            .WithMessage("Qamrov uchun nishon tanlanishi kerak.");
        RuleFor(x => x.EndsOn).GreaterThanOrEqualTo(x => x.StartsOn!.Value)
            .When(x => x.StartsOn is not null && x.EndsOn is not null);
    }
}
