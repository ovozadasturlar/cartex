namespace Cartex.Domain.Enums;

public enum AccountType
{
    Cash,
    Card,
    Transfer,
    Bank,
    Bonus,
    Debt,
    CustomerAdvance,
    RewardRecovery
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
    Change,
    CustomerCredit,
    CustomerPayment,
    CustomerAdvance,
    CustomerRefund,
    SaleReturn,
    CashbackRecovery,
    PartnerRewardCash,
    PartnerRewardBonus,
    DebtWriteOff
}

public enum CustomerPaymentAllocationKind
{
    Payment,
    WriteOff
}

public enum PaymentMethod
{
    Cash,
    Card,
    Bonus,
    Transfer,
    Bank
}

public enum BusinessDocumentStatus
{
    Posted,
    Voided
}

public enum ReturnItemCondition
{
    Sellable,
    Opened,
    Damaged,
    Defective
}

public enum InventoryDisposition
{
    SellableRestock,
    Quarantine,
    Scrap,
    SupplierClaim
}

public enum ReturnSettlementMethod
{
    ReduceDebt,
    Cash,
    Card,
    Bonus,
    CustomerAdvance,
    NoCharge
}

public enum InventoryLocationKind
{
    External,
    Warehouse,
    Customer,
    Quarantine,
    Scrap,
    SupplierClaim
}

public enum InventoryMovementKind
{
    SaleIssue,
    SaleReturn,
    SupplyReceipt,
    Transfer,
    Adjustment,
    PartnerReward
}

public enum PartnerRewardMode
{
    Points,
    Cash,
    Bonus,
    Product
}

public enum PartnerRewardBasis
{
    NetRevenue,
    NetMargin,
    FixedPerUnit,
    FixedPerSale
}

public enum PartnerRewardTrigger
{
    Sale,
    Settlement,
    Payment
}

public enum PartnerRewardState
{
    Pending,
    Earned,
    Reversed,
    Redeemed
}

public enum ParticipantAttributionSource
{
    Direct,
    CaseInherited,
    CartInherited
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
    PartialReturn,
    Voided
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
