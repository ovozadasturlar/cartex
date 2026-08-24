using System.Security.Cryptography;
using System.Text;
using Cartex.Application.Common.Interfaces;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Sms;

public sealed class SmsGatewayService(IApplicationDbContext db, SmsGatewayRoutingService routing)
{
    public async Task<SmsGatewayJob?> CreateAsync(
        long branchId,
        SmsGatewayJobKind kind,
        string phone,
        string text,
        int segmentCount,
        string idempotencyKey,
        long? customerId,
        CancellationToken cancellationToken,
        long? notificationDeliveryId = null,
        long? notificationAttemptId = null,
        long? retryOfJobId = null)
    {
        var existing = await db.SmsGatewayJobs.FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null)
            return existing;

        if (kind == SmsGatewayJobKind.Promotion)
        {
            if (customerId is null)
                return null;
            var allowed = await db.Customers.Where(x => x.Id == customerId)
                .Select(x => x.AllowMarketingSms && !x.NotificationsOptOut)
                .SingleOrDefaultAsync(cancellationToken);
            if (!allowed)
                return null;
        }

        NotificationDelivery delivery;
        NotificationDeliveryAttempt attempt;
        if (notificationDeliveryId is long deliveryId)
        {
            delivery = await db.NotificationDeliveries.Include(x => x.Attempts)
                .SingleAsync(x => x.Id == deliveryId, cancellationToken);
            attempt = notificationAttemptId is long attemptId
                ? delivery.Attempts.Single(x => x.Id == attemptId)
                : delivery.Attempts.OrderByDescending(x => x.AttemptNumber).First();
        }
        else
        {
            delivery = new NotificationDelivery
            {
                CustomerId = customerId,
                Channel = NotificationChannel.Sms,
                Purpose = kind.ToString(),
                Recipient = phone,
                Content = text
            };
            attempt = new NotificationDeliveryAttempt
            {
                NotificationDelivery = delivery,
                AttemptNumber = 1,
                Provider = "device",
                Units = Math.Max(1, segmentCount)
            };
            delivery.Attempts.Add(attempt);
            db.NotificationDeliveries.Add(delivery);
        }

        var job = new SmsGatewayJob
        {
            BranchId = branchId,
            Kind = kind,
            Phone = phone,
            Text = text,
            CustomerId = customerId,
            NotificationDelivery = delivery,
            NotificationDeliveryAttempt = attempt,
            SegmentCount = Math.Max(1, segmentCount),
            IdempotencyKey = idempotencyKey,
            RetryOfJobId = retryOfJobId
        };
        db.SmsGatewayJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        await routing.AssignAsync(job, cancellationToken);
        return job;
    }

    public async Task RequeueExpiredAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var jobs = await db.SmsGatewayJobs
            .Where(x => x.Status == SmsGatewayJobStatus.Assigned && x.LeaseExpiresAt < now)
            .ToListAsync(cancellationToken);
        foreach (var job in jobs)
            ClearAssignment(job);
        if (jobs.Count > 0)
            await db.SaveChangesAsync(cancellationToken);
        foreach (var job in jobs)
            await routing.AssignAsync(job, cancellationToken);
    }

    public static void ClearAssignment(SmsGatewayJob job)
    {
        job.Status = SmsGatewayJobStatus.Pending;
        job.AssignedDeviceId = null;
        job.LeaseToken = null;
        job.LeaseExpiresAt = null;
        job.AssignedAt = null;
        job.WaitingReason = null;
        job.AvailableAt = null;
        SmsGatewayDeliverySync.Apply(job);
    }
}

public static class SmsGatewayDeliverySync
{
    public static void Apply(SmsGatewayJob job)
    {
        if (job.NotificationDelivery is null || job.NotificationDeliveryAttempt is null)
            return;
        var status = job.Status switch
        {
            SmsGatewayJobStatus.Pending or SmsGatewayJobStatus.Assigned => NotificationDeliveryStatus.Pending,
            SmsGatewayJobStatus.Sent => NotificationDeliveryStatus.Sent,
            SmsGatewayJobStatus.Delivered => NotificationDeliveryStatus.Delivered,
            SmsGatewayJobStatus.Simulated => NotificationDeliveryStatus.Simulated,
            SmsGatewayJobStatus.Failed or SmsGatewayJobStatus.Rejected => NotificationDeliveryStatus.Failed,
            SmsGatewayJobStatus.Cancelled => NotificationDeliveryStatus.Cancelled,
            _ => throw new InvalidOperationException($"Noma'lum SMS holati: {job.Status}")
        };
        var now = DateTime.UtcNow;
        job.NotificationDelivery.Status = status;
        job.NotificationDeliveryAttempt.Status = status;
        job.NotificationDeliveryAttempt.ErrorCode = job.ErrorCode;
        job.NotificationDeliveryAttempt.ErrorMessage = job.ErrorMessage;
        if (status == NotificationDeliveryStatus.Sent)
        {
            job.NotificationDelivery.AcceptedAt = job.SentAt ?? now;
            job.NotificationDeliveryAttempt.AcceptedAt = job.SentAt ?? now;
        }
        if (status == NotificationDeliveryStatus.Delivered)
        {
            job.NotificationDelivery.DeliveredAt = job.DeliveredAt ?? now;
            job.NotificationDeliveryAttempt.DeliveredAt = job.DeliveredAt ?? now;
        }
        if (status is NotificationDeliveryStatus.Delivered or NotificationDeliveryStatus.Failed
            or NotificationDeliveryStatus.Cancelled or NotificationDeliveryStatus.Simulated)
        {
            job.NotificationDelivery.CompletedAt = now;
            job.NotificationDeliveryAttempt.CompletedAt = now;
        }
    }
}

public static class SmsGatewayCredential
{
    public static string Issue() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static bool Matches(string hash, string token) => CryptographicOperations.FixedTimeEquals(
        Convert.FromHexString(hash), Convert.FromHexString(Hash(token)));

    public static void Ensure(SmsGatewayDevice device, string token)
    {
        if (string.IsNullOrWhiteSpace(token) || !Matches(device.CredentialHash, token))
            throw new ForbiddenException("SMS shlyuzi credential noto'g'ri.");
    }
}
