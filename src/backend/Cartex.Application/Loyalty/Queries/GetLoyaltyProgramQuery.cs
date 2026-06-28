using Cartex.Domain.Enums;
using Cartex.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Loyalty.Queries;

public record GetLoyaltyProgramQuery : IRequest<LoyaltyProgramDto>;

public record LoyaltyProgramDto(bool IsEnabled, string Base, decimal TotalPercent);

public sealed class GetLoyaltyProgramQueryHandler(IApplicationDbContext db) : IRequestHandler<GetLoyaltyProgramQuery, LoyaltyProgramDto>
{
    public async Task<LoyaltyProgramDto> Handle(GetLoyaltyProgramQuery request, CancellationToken cancellationToken)
    {
        var program = await db.LoyaltyPrograms.FirstOrDefaultAsync(p => p.BranchId == null, cancellationToken);

        return program is null
            ? new LoyaltyProgramDto(false, CashbackBase.None.ToString(), 0)
            : new LoyaltyProgramDto(program.IsEnabled, program.Base.ToString(), program.TotalPercent);
    }
}
