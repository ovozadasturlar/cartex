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
