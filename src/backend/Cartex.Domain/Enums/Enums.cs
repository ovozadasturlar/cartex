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
    SupplyPay,
    CashIn,
    CashOut,
    Change
}

public enum PaymentMethod
{
    Cash,
    Card,
    Bonus
}

public enum ShiftStatus
{
    Open,
    Closed
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

public enum UnitDimension
{
    Count,
    Weight,
    Volume,
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
    Confirmed,
    Ready,
    CheckedOut,
    Cancelled
}
