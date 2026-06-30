using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class ExpenseCategory : SoftDeleteEntity
{
    public string Name { get; set; } = null!;
}
