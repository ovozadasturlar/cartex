using SQLite;

namespace Cartex.Mobile.Agent.Data;

public class LocalCustomer
{
    [PrimaryKey] public long Id { get; set; }
    public string FullName { get; set; } = "";
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public decimal DebtBalance { get; set; }
    public decimal? CreditLimit { get; set; }
    public string DebtBalancesJson { get; set; } = "[]";
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public class LocalVanStock
{
    [PrimaryKey] public long VariantId { get; set; }
    public string ProductName { get; set; } = "";
    public long? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string UnitName { get; set; } = "";
    public string Dimension { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal SellingPrice { get; set; }
    public string? ImageUrl { get; set; }
    public decimal? DiscountPct { get; set; }
    public string? Code { get; set; }
    public string Barcodes { get; set; } = "";
}

public class OutboxItem
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public string Kind { get; set; } = "";
    public string Key { get; set; } = "";
    public string PayloadJson { get; set; } = "";
    public string Status { get; set; } = "pending";
    public string? Error { get; set; }
    public string? ReceiptToken { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class LocalOrder
{
    [PrimaryKey] public string LocalId { get; set; } = "";
    public string? Code { get; set; }
    public long? CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string ItemsJson { get; set; } = "[]";
    public decimal Total { get; set; }
    public string Status { get; set; } = "new";
    public DateTime CreatedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
}

public class MetaEntry
{
    [PrimaryKey] public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}
