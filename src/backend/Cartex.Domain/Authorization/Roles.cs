namespace Cartex.Domain.Authorization;

public static class AppRoles
{
    public const string Developer = "developer";
    public const string Admin = "admin";
    public const string Seller = "seller";
    public const string SellerAssistant = "seller_assistant";
    public const string Cashier = "cashier";
    public const string Accountant = "accountant";
    public const string WarehouseOperator = "warehouse_operator";
    public const string SupplyOperator = "supply_operator";
    public const string Agent = "agent";

    public const int DeveloperLevel = 1000;
    public const int AdminLevel = 100;
    public const int AccountantLevel = 30;
    public const int WarehouseOperatorLevel = 20;
    public const int SupplyOperatorLevel = 20;
    public const int SellerLevel = 10;
    public const int SellerAssistantLevel = 10;
    public const int CashierLevel = 10;
    public const int AgentLevel = 10;
}
