using Cartex.Domain.Common;
using Cartex.Domain.Enums;

namespace Cartex.Domain.Entities;

public class Transaction : AuditableEntity
{
    public long? FromAccountId { get; set; }
    public Account? FromAccount { get; set; }

    public long? ToAccountId { get; set; }
    public Account? ToAccount { get; set; }

    public decimal Amount { get; set; }
    public OperationType OperationType { get; set; }

    public long? SaleId { get; set; }
    public Sale? Sale { get; set; }

    public long? SupplyId { get; set; }
    public Supply? Supply { get; set; }

    public long UserId { get; set; }
    public User User { get; set; } = null!;
}
