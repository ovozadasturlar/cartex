namespace Cartex.Domain.Enums;

public enum AccountType
{
    Cash,
    Card,
    Bonus,
    Debt
}

public enum OperationType
{
    Sale,
    DebtCharge,
    DebtPay,
    Cashback,
    BonusSpend,
    SupplyPay
}

public enum TransferStatus
{
    Sent,
    Received,
    Cancelled
}

public enum SaleStatus
{
    Completed,
    Returned,
    PartialReturn
}

public enum CashbackBase
{
    None,
    PercentOfTotal,
    PerLineRules
}

public enum CashbackScope
{
    Product,
    Category
}

public enum CashbackMethod
{
    Percent,
    FixedPerUnit
}

public enum MeasureMode
{
    Counted,
    Weighed,
    Length
}

public enum OutboxStatus
{
    Pending,
    Processed,
    Failed
}

public enum CartStatus
{
    Open,
    CheckedOut,
    Cancelled
}
