using Cartex.Domain.Enums;

namespace Cartex.Application.Common.Settings;

public sealed class TradeCaseSettings
{
    public bool Enabled { get; set; } = true;
    public string SingularLabel { get; set; } = "Loyiha";
    public string PluralLabel { get; set; } = "Loyihalar";
    public TradeCaseWorkflow DefaultWorkflow { get; set; } = TradeCaseWorkflow.CustodyUntilSettlement;
    public TradeCasePricePolicy DefaultPricePolicy { get; set; } = TradeCasePricePolicy.SnapshotAtIssue;
    public bool AllowWorkflowOverride { get; set; } = true;
    public bool AllowPricePolicyOverride { get; set; } = true;
    public bool RequireSiteAddress { get; set; }
    public bool AutoUseCustomerAdvance { get; set; } = true;
}
