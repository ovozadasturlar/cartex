namespace Cartex.Shared.Models.AuditLogs;

public record AuditFilterOptionsDto(List<string> Tables, List<string> Actions, List<string> Users);
