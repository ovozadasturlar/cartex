using Cartex.Application.Common.Messaging;
using FluentValidation;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.Loyalty.Commands;

public record CreateCashbackRuleCommand(CashbackScope Scope, long TargetId, CashbackMethod Method, decimal Value, int Priority, bool ExcludeFromTotalPercent) : ICommand<long>;

public record UpdateCashbackRuleCommand(long Id, CashbackScope Scope, long TargetId, CashbackMethod Method, decimal Value, int Priority, bool ExcludeFromTotalPercent) : ICommand<Unit>;

public record DeleteCashbackRuleCommand(long Id) : ICommand<Unit>;

public sealed class CreateCashbackRuleCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateCashbackRuleCommand, long>
{
    public async Task<long> Handle(CreateCashbackRuleCommand request, CancellationToken cancellationToken)
    {
        var program = await db.LoyaltyPrograms.FirstOrDefaultAsync(p => p.BranchId == null, cancellationToken);
        if (program is null)
        {
            program = new LoyaltyProgram();
            db.LoyaltyPrograms.Add(program);
            await db.SaveChangesAsync(cancellationToken);
        }

        var rule = new CashbackRule
        {
            LoyaltyProgramId = program.Id,
            Scope = request.Scope,
            TargetId = request.TargetId,
            Method = request.Method,
            Value = request.Value,
            Priority = request.Priority,
            ExcludeFromTotalPercent = request.ExcludeFromTotalPercent
        };
        db.CashbackRules.Add(rule);
        await db.SaveChangesAsync(cancellationToken);
        return rule.Id;
    }
}

public sealed class UpdateCashbackRuleCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateCashbackRuleCommand, Unit>
{
    public async Task<Unit> Handle(UpdateCashbackRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await db.CashbackRules.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Cashback rule not found.");

        rule.Scope = request.Scope;
        rule.TargetId = request.TargetId;
        rule.Method = request.Method;
        rule.Value = request.Value;
        rule.Priority = request.Priority;
        rule.ExcludeFromTotalPercent = request.ExcludeFromTotalPercent;

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class DeleteCashbackRuleCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteCashbackRuleCommand, Unit>
{
    public async Task<Unit> Handle(DeleteCashbackRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await db.CashbackRules.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Cashback rule not found.");

        db.CashbackRules.Remove(rule);
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class CreateCashbackRuleCommandValidator : AbstractValidator<CreateCashbackRuleCommand>
{
    public CreateCashbackRuleCommandValidator()
    {
        RuleFor(x => x.TargetId).GreaterThan(0);
        RuleFor(x => x.Value).GreaterThan(0);
    }
}

public sealed class UpdateCashbackRuleCommandValidator : AbstractValidator<UpdateCashbackRuleCommand>
{
    public UpdateCashbackRuleCommandValidator()
    {
        RuleFor(x => x.TargetId).GreaterThan(0);
        RuleFor(x => x.Value).GreaterThan(0);
    }
}
