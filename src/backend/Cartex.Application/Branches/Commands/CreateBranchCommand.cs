using MediatR;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Domain.Entities;

namespace Cartex.Application.Branches.Commands;

public record CreateBranchCommand(string Name, string? Address, string? Phone) : ICommand<long>;

public sealed class CreateBranchCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateBranchCommand, long>
{
    public async Task<long> Handle(CreateBranchCommand request, CancellationToken cancellationToken)
    {
        var businessId = await db.Businesses.Select(b => b.Id).FirstAsync(cancellationToken);

        var branch = new Branch
        {
            BusinessId = businessId,
            Name = request.Name,
            Address = request.Address,
            Phone = request.Phone
        };

        db.Branches.Add(branch);
        await db.SaveChangesAsync(cancellationToken);

        return branch.Id;
    }
}

public sealed class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}
