using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class Customer : SoftDeleteEntity
{
    public long PartyId { get; set; }
    public Party Party { get; set; } = null!;
    public string? LastName { get; set; }
    public string? CardBarcode { get; set; }
    public string? TelegramChatId { get; set; }
    public string? PreferredLanguage { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    /// <summary>Internal employee responsible for this customer; never an external referrer.</summary>
    public long? AssignedUserId { get; set; }
    public User? AssignedUser { get; set; }
    public decimal DiscountPct { get; set; }
    /// Bo'sh — qarz chegarasi yo'q, 0 — bu mijozga qarzga sotilmaydi (SOZ-02a).
    public decimal? CreditLimit { get; set; }
    public bool NotificationsOptOut { get; set; }
    public bool AllowMarketingSms { get; set; }

    public ICollection<Account> Accounts { get; set; } = [];
    public ICollection<Sale> Sales { get; set; } = [];
}
