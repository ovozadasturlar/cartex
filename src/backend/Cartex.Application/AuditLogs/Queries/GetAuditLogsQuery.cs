using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.AuditLogs.Queries;

public record GetAuditLogsQuery : FilteringRequest, IRequest<IReadOnlyCollection<AuditLogDto>>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? TableName { get; set; }
    public string? UserName { get; set; }
    public string? Action { get; set; }
}

public record AuditLogDto(
    long Id,
    string? UserName,
    string Action,
    string TableName,
    long? RecordId,
    string? OldData,
    string? NewData,
    DateTime CreatedAt,
    string? Client,
    Guid EventId,
    string? Summary,
    string? CommandName,
    string? Details,
    int EntityCount,
    long? BranchId,
    string? DeviceId,
    string? DeviceName,
    string? IpAddress,
    string? CorrelationId);

public sealed class GetAuditLogsQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetAuditLogsQuery, IReadOnlyCollection<AuditLogDto>>
{
    public async Task<IReadOnlyCollection<AuditLogDto>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var query = db.AuditLogs
            .Include(a => a.User)
            .AsQueryable();

        if (request.FromDate is { } fromDate)
            query = query.Where(a => a.CreatedAt >= DateTime.SpecifyKind(fromDate, DateTimeKind.Utc));
        if (request.ToDate is { } toDate)
            query = query.Where(a => a.CreatedAt < DateTime.SpecifyKind(toDate, DateTimeKind.Utc));
        if (!string.IsNullOrWhiteSpace(request.TableName))
        {
            var table = request.TableName.ToLower();
            query = query.Where(a => a.TableName.ToLower().Contains(table));
        }
        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            var action = request.Action.ToLower();
            query = query.Where(a => a.Action.ToLower().Contains(action));
        }
        if (!string.IsNullOrWhiteSpace(request.UserName))
        {
            var userName = request.UserName.ToLower();
            query = query.Where(a => a.User != null && a.User.FullName.ToLower().Contains(userName));
        }

        return await query
            .ToPagedListAsync(request,
                a => new AuditLogDto(
                    a.Id,
                    a.User != null ? a.User.FullName : null,
                    a.Action,
                    a.TableName,
                    a.RecordId,
                    a.OldData,
                    a.NewData,
                    a.CreatedAt,
                    a.Client,
                    a.EventId,
                    a.Summary,
                    a.CommandName,
                    a.Details,
                    a.EntityCount,
                    a.BranchId,
                    a.DeviceId,
                    a.DeviceName,
                    a.IpAddress,
                    a.CorrelationId),
                writer, cancellationToken);
    }
}
