using Cartex.Domain.Entities;

namespace Cartex.Application.Customers.Queries;

/// Mijozning kimligi Party'da, sozlamalari customers'da. Ro'yxat so'rovlari filtr/qidiruv/tartibni
/// shu yassi qatorga qo'llaydi: `AsFilterable` refleksiya bilan faqat yuqori darajadagi maydonlarni
/// ko'radi, ya'ni navigatsiya ortidagi ism va telefon aks holda qidiruvdan tushib qolardi.
/// Proyeksiya ataylab obyekt initsializatori bilan yoziladi — konstruktorli qatorda EF `Where`
/// shartini `Select` ichiga kirita olmaydi va so'rov tarjima qilinmaydi.
public sealed class CustomerRow
{
    public long Id { get; init; }
    public long PartyId { get; init; }
    public string FullName { get; init; } = null!;
    public string? SearchFold { get; init; }
    public string? LastName { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? CardBarcode { get; init; }
    public decimal DiscountPct { get; init; }
    public decimal? CreditLimit { get; init; }
    public bool NotificationsOptOut { get; init; }
    public bool HasTelegram { get; init; }
    public string? PreferredLanguage { get; init; }
    public bool AllowMarketingSms { get; init; }
}

public static class CustomerRows
{
    public static IQueryable<CustomerRow> AsRows(this IQueryable<Customer> customers) =>
        customers.Select(c => new CustomerRow
        {
            Id = c.Id,
            PartyId = c.PartyId,
            FullName = c.Party.FullName,
            SearchFold = c.Party.SearchFold,
            LastName = c.LastName,
            Address = c.Party.Address,
            Phone = c.Party.Phone,
            Email = c.Party.Email,
            CreatedAt = c.CreatedAt,
            CardBarcode = c.CardBarcode,
            DiscountPct = c.DiscountPct,
            CreditLimit = c.CreditLimit,
            NotificationsOptOut = c.NotificationsOptOut,
            HasTelegram = c.TelegramChatId != null,
            PreferredLanguage = c.PreferredLanguage,
            AllowMarketingSms = c.AllowMarketingSms
        });
}
