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

public enum DiscountScope
{
    All,
    Product,
    Category,
    Manufacturer
}

public enum DiscountMethod
{
    Percent,
    FixedAmount
}

public enum DiscountCombineMode
{
    Priority,
    Stack
}

public enum UnitDimension
{
    Count,
    Weight,
    Volume,
    Length
}

public enum PackKind
{
    Purchase,
    Sale,
    Both
}

public enum SupplyPriceBasis
{
    PerEntry,
    PerStockingUnit
}

public enum OutboxStatus
{
    Pending,
    Processed,
    Failed
}

public enum PrepackStatus
{
    Active,
    Sold,
    Expired
}

public enum SmsStatus
{
    Sent,
    Failed,
    Delivered,
    Undelivered
}

public enum CartKind
{
    Queue,
    Order
}

public enum CartStatus
{
    Open,
    Confirmed,
    Ready,
    CheckedOut,
    Cancelled
}
