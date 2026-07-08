namespace Cartex.Shared.Models.Settings;

public record SmsMessageDto(long Id, string Phone, string Text, string Provider, string Status, int Segments, string? Error, DateTime CreatedAt, DateTime? DeliveredAt);

public record SmsStatsDto(int Total, int Sent, int Delivered, int Undelivered, int Failed, int Segments);
