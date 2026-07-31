using System.Text.Json;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Domain.Events;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cartex.Infrastructure.Notifications;

public sealed class DebtReminderScheduler(IServiceProvider services, ILogger<DebtReminderScheduler> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
                var cfg = await settings.GetAsync<ReminderSettings>(SettingKeys.Reminder, stoppingToken);

                if (cfg is { Enabled: true } && IsInWindow(DateTime.Now.Hour, cfg.SendHourLocal))
                {
                    var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                    var enqueued = await EnqueueDueRemindersAsync(db, cfg, DateTime.UtcNow, stoppingToken);
                    if (enqueued > 0)
                        logger.LogInformation("Enqueued {Count} debt reminders", enqueued);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Debt reminder cycle failed");
            }

            try { await Task.Delay(PollInterval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    public static bool IsInWindow(int hourLocal, int sendHour) =>
        hourLocal >= sendHour && hourLocal < sendHour + 2;

    public static async Task<int> EnqueueDueRemindersAsync(IApplicationDbContext db, ReminderSettings cfg, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var accounts = await db.Accounts
            .Where(a => a.Type == AccountType.Debt && a.CustomerId != null && a.Balance > 0 && !a.Customer!.NotificationsOptOut)
            .Select(a => new { a.Id, CustomerId = a.CustomerId!.Value, CustomerName = a.Customer!.FullName, a.Balance, a.Currency })
            .ToListAsync(cancellationToken);

        if (accounts.Count == 0)
            return 0;

        var accountIds = accounts.Select(a => a.Id).ToList();
        var activity = await db.Transactions
            .Where(t => t.OperationType == OperationType.DebtCharge || t.OperationType == OperationType.DebtPay)
            .Where(t => (t.FromAccountId != null && accountIds.Contains(t.FromAccountId.Value))
                     || (t.ToAccountId != null && accountIds.Contains(t.ToAccountId.Value)))
            .GroupBy(t => t.FromAccountId != null && accountIds.Contains(t.FromAccountId.Value) ? t.FromAccountId!.Value : t.ToAccountId!.Value)
            .Select(g => new { AccountId = g.Key, Last = g.Max(t => t.CreatedAt) })
            .ToListAsync(cancellationToken);

        var lastByAccount = activity.ToDictionary(a => a.AccountId, a => a.Last);

        var customerIds = accounts.Select(a => a.CustomerId).Distinct().ToList();
        var today = DateOnly.FromDateTime(nowUtc);
        var dueDates = await db.Sales
            .Where(s => s.CustomerId != null && customerIds.Contains(s.CustomerId.Value) && s.DebtDueDate != null && s.DebtAmount > s.RefundedDebt)
            .GroupBy(s => s.CustomerId!.Value)
            .Select(g => new { CustomerId = g.Key, DueDate = g.Min(s => s.DebtDueDate!.Value) })
            .ToListAsync(cancellationToken);
        var dueByCustomer = dueDates.ToDictionary(d => d.CustomerId, d => d.DueDate);
        var logWindow = nowUtc.AddDays(-Math.Max(cfg.RepeatEveryDays, cfg.DaysBeforeDue + 2));
        var recentLogs = await db.DebtReminderLogs
            .Where(l => customerIds.Contains(l.CustomerId) && l.SentAt > logWindow)
            .Select(l => new { l.CustomerId, l.SentAt, l.Purpose, l.DueDate })
            .ToListAsync(cancellationToken);

        var enqueued = 0;
        foreach (var group in accounts.GroupBy(a => a.CustomerId))
        {
            var hasDueDate = dueByCustomer.TryGetValue(group.Key, out var dueDate);
            var due = group
                .Select(a => new
                {
                    a.CustomerName,
                    a.Balance,
                    a.Currency,
                    Days = lastByAccount.TryGetValue(a.Id, out var last) ? (int)(nowUtc - last).TotalDays : 0
                })
                .Where(a => a.Balance >= cfg.MinBalance)
                .Where(a => hasDueDate || a.Days >= cfg.MinDaysOverdue)
                .OrderByDescending(a => a.Balance)
                .FirstOrDefault();

            if (due is null)
                continue;

            var daysUntilDue = hasDueDate ? dueDate.DayNumber - today.DayNumber : int.MinValue;
            var template = hasDueDate
                ? daysUntilDue == cfg.DaysBeforeDue && cfg.NotifyBeforeDue
                    ? "debt_due_soon"
                    : daysUntilDue == 0 && cfg.NotifyOnDueDate
                        ? "debt_due_today"
                        : daysUntilDue < 0
                            ? "debt_reminder"
                            : null
                : "debt_reminder";
            if (template is null)
                continue;

            var alreadySent = template == "debt_reminder"
                ? recentLogs.Any(l => l.CustomerId == group.Key
                    && l.Purpose == template
                    && l.SentAt > nowUtc.AddDays(-cfg.RepeatEveryDays))
                : recentLogs.Any(l => l.CustomerId == group.Key
                    && l.Purpose == template
                    && l.DueDate == dueDate);
            if (alreadySent)
                continue;

            var evt = new DebtReminderDueEvent(group.Key, due.CustomerName, due.Balance, due.Currency, due.Days, template, hasDueDate ? dueDate : null);
            db.NotificationOutbox.Add(new NotificationOutbox
            {
                EventType = typeof(DebtReminderDueEvent).FullName!,
                Payload = JsonSerializer.Serialize(evt)
            });
            db.DebtReminderLogs.Add(new DebtReminderLog
            {
                CustomerId = group.Key,
                SentAt = nowUtc,
                Balance = due.Balance,
                DaysOverdue = hasDueDate ? Math.Max(0, -daysUntilDue) : due.Days,
                Purpose = template,
                DueDate = hasDueDate ? dueDate : null
            });
            enqueued++;
        }

        if (enqueued > 0)
            await db.SaveChangesAsync(cancellationToken);

        return enqueued;
    }
}
