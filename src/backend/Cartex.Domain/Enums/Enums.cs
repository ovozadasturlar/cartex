namespace Cartex.Domain.Enums;

public enum AccountOwnerType
{
    Branch,
    Customer,
    Supplier
}

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
    Bonus,
    DebtPay,
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
