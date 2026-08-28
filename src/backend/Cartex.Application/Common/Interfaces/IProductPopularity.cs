namespace Cartex.Application.Common.Interfaces;

public interface IProductPopularity
{
    Task<IReadOnlyDictionary<long, int>> GetRanksAsync(long branchId, CancellationToken cancellationToken = default);
}
