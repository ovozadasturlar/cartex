using Cartex.Domain.Common;
using Cartex.Domain.Enums;

namespace Cartex.Domain.Entities;

public class DiscountRule : SoftDeleteEntity
{
    public string Name { get; set; } = null!;
    public bool IsEnabled { get; set; } = true;
    public DiscountScope Scope { get; set; }
    public long? TargetId { get; set; }
    public long? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public decimal MinAmount { get; set; }
    public DiscountMethod Method { get; set; }
    public decimal Value { get; set; }
    public int Priority { get; set; }
    public DateOnly? StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }

    public ICollection<DiscountRuleException> Exceptions { get; set; } = [];
}

public class DiscountRuleException : BaseEntity
{
    public long DiscountRuleId { get; set; }
    public DiscountRule DiscountRule { get; set; } = null!;
    public long ProductId { get; set; }
    public Product Product { get; set; } = null!;
}
