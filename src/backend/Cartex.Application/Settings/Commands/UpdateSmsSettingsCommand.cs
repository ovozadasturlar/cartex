using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Application.Sms;
using Cartex.Persistence;
using FluentValidation;

namespace Cartex.Application.Settings.Commands;

public record UpdateSmsSettingsCommand(
    bool Enabled, string Provider, string? Login, string? Password, string? Sender, string? BaseUrl,
    string FallbackProvider = "none", int FallbackAfterMinutes = 30, bool DebtReminderEnabled = true,
    bool ReceiptLinkEnabled = false, bool PromotionEnabled = false, bool ManualEnabled = true,
    string? DebtReminderTemplate = null, string? ReceiptLinkTemplate = null,
    string? PromotionTemplate = null, string? ManualTemplate = null,
    bool SendReceiptOnSale = false,
    bool TestMode = true,
    IReadOnlyList<string>? TestAllowedNumbers = null,
    int DebtReminderStickyWaitMinutes = 15,
    int ReceiptLinkStickyWaitMinutes = 0,
    int PromotionStickyWaitMinutes = 60,
    int ManualStickyWaitMinutes = 0,
    bool QuietHoursEnabled = true,
    string SendWindowStart = "09:00",
    string SendWindowEnd = "21:00") : ICommand<Unit>;

public sealed class UpdateSmsSettingsCommandHandler(ISettingsService settings, ISecretProtector protector, IAuditService audit)
    : IRequestHandler<UpdateSmsSettingsCommand, Unit>
{
    public async Task<Unit> Handle(UpdateSmsSettingsCommand request, CancellationToken cancellationToken)
    {
        var cfg = await settings.GetAsync<SmsSettings>(SettingKeys.Sms, cancellationToken) ?? new();
        cfg.Enabled = request.Enabled;
        cfg.Provider = request.Provider;
        cfg.FallbackProvider = request.Provider == "device" ? request.FallbackProvider : "none";
        cfg.FallbackAfterMinutes = cfg.FallbackProvider == "none" ? 30 : request.FallbackAfterMinutes;
        var usesAggregator = request.Provider != "device" || cfg.FallbackProvider != "none";
        cfg.Login = usesAggregator ? request.Login : null;
        cfg.Sender = usesAggregator ? request.Sender : null;
        cfg.BaseUrl = usesAggregator ? request.BaseUrl : null;
        cfg.DebtReminderEnabled = request.DebtReminderEnabled;
        cfg.ReceiptLinkEnabled = request.ReceiptLinkEnabled;
        cfg.PromotionEnabled = request.PromotionEnabled;
        cfg.ManualEnabled = request.ManualEnabled;
        cfg.SendReceiptOnSale = request.SendReceiptOnSale;
        cfg.TestMode = request.TestMode;
        cfg.TestAllowedNumbers = request.TestAllowedNumbers?.Select(SmsTestModePolicy.NormalizePhone)
            .Where(x => x.Length >= 9).Distinct(StringComparer.Ordinal).Take(50).ToList() ?? [];
        cfg.DebtReminderStickyWaitMinutes = request.DebtReminderStickyWaitMinutes;
        cfg.ReceiptLinkStickyWaitMinutes = request.ReceiptLinkStickyWaitMinutes;
        cfg.PromotionStickyWaitMinutes = request.PromotionStickyWaitMinutes;
        cfg.ManualStickyWaitMinutes = request.ManualStickyWaitMinutes;
        cfg.QuietHoursEnabled = request.QuietHoursEnabled;
        cfg.SendWindowStart = request.SendWindowStart;
        cfg.SendWindowEnd = request.SendWindowEnd;
        cfg.DebtReminderTemplate = request.DebtReminderTemplate ?? cfg.DebtReminderTemplate;
        cfg.ReceiptLinkTemplate = request.ReceiptLinkTemplate ?? cfg.ReceiptLinkTemplate;
        cfg.PromotionTemplate = request.PromotionTemplate ?? cfg.PromotionTemplate;
        cfg.ManualTemplate = request.ManualTemplate ?? cfg.ManualTemplate;
        if (!usesAggregator)
            cfg.Password = null;
        else if (!string.IsNullOrWhiteSpace(request.Password))
            cfg.Password = protector.Protect(request.Password);

        audit.Add("settings", "settings", null, new { section = "sms" });
        await settings.SetAsync(SettingKeys.Sms, cfg, cancellationToken);
        return Unit.Value;
    }
}

public sealed class UpdateSmsSettingsCommandValidator : AbstractValidator<UpdateSmsSettingsCommand>
{
    public UpdateSmsSettingsCommandValidator()
    {
        RuleFor(x => x.Provider).Must(x => x is "device" or "eskiz" or "playmobile");
        RuleFor(x => x.FallbackProvider).Must(x => x is "none" or "eskiz" or "playmobile");
        RuleFor(x => x.FallbackAfterMinutes).GreaterThanOrEqualTo(1)
            .When(x => x.Provider == "device" && x.FallbackProvider != "none");
        RuleFor(x => x.DebtReminderTemplate).MaximumLength(1000);
        RuleFor(x => x.ReceiptLinkTemplate).MaximumLength(1000);
        RuleFor(x => x.PromotionTemplate).MaximumLength(1000);
        RuleFor(x => x.ManualTemplate).MaximumLength(1000);
        RuleForEach(x => x.TestAllowedNumbers).MaximumLength(30);
        RuleFor(x => x.DebtReminderStickyWaitMinutes).InclusiveBetween(0, 1_440);
        RuleFor(x => x.ReceiptLinkStickyWaitMinutes).InclusiveBetween(0, 1_440);
        RuleFor(x => x.PromotionStickyWaitMinutes).InclusiveBetween(0, 1_440);
        RuleFor(x => x.ManualStickyWaitMinutes).InclusiveBetween(0, 1_440);
        RuleFor(x => x.SendWindowStart).Must(IsTime);
        RuleFor(x => x.SendWindowEnd).Must(IsTime);
    }

    private static bool IsTime(string value) => TimeOnly.TryParseExact(value, "HH:mm", out _);
}
