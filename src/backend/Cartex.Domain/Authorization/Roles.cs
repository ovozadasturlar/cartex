namespace Cartex.Domain.Authorization;

public static class AppRoles
{
    public const string Developer = "developer";
    public const string Admin = "admin";
    public const string Seller = "seller";
    public const string Agent = "agent";

    public const int DeveloperLevel = 1000;
    public const int AdminLevel = 100;
    public const int SellerLevel = 10;
    public const int AgentLevel = 10;
}
