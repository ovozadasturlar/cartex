using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Shared.Models.SmsGateway;
using Microsoft.AspNetCore.SignalR.Client;

namespace Cartex.Mobile.Store.Services;

public sealed class SmsGatewayHostService(
    ISmsGatewayApi api,
    ISmsGatewayPlatform platform,
    SessionStore session,
    MobileAuthService auth) : IAsyncDisposable
{
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private CancellationTokenSource? _lifetime;
    private HubConnection? _hub;
    private Task? _loop;
    private int _polling;
    private int _pollAgain;

    public bool IsRunning => _lifetime is not null;

    public async Task StartAsync()
    {
        if (IsRunning)
            return;
        await session.LoadAsync();
        if (session.AccessToken is null || (await RegistrationsAsync()).Count == 0)
            return;
        if (!await platform.EnsurePermissionAsync())
            throw new InvalidOperationException(Loc.Instance["sms_gateway_permission_required"]);
        _lifetime = new CancellationTokenSource();
        SmsForegroundControl.Start?.Invoke();
        _loop = RunAsync(_lifetime.Token);
    }

    public async Task StopAsync()
    {
        var lifetime = _lifetime;
        _lifetime = null;
        if (lifetime is null)
            return;
        await lifetime.CancelAsync();
        if (_loop is not null)
        {
            try
            {
                await _loop;
            }
            catch (OperationCanceledException)
            {
            }
        }
        if (_hub is not null)
            await _hub.DisposeAsync();
        _hub = null;
        lifetime.Dispose();
        SmsForegroundControl.Stop?.Invoke();
    }

    public async Task<RegisterSmsGatewayResult> RegisterAsync(
        MobileSimInfo sim,
        string? phoneLabel,
        SmsGatewayConsentScope consent)
    {
        var token = await SecureStorage.GetAsync(TokenKey(sim.Slot));
        var branchId = auth.DefaultBranchId ?? throw new InvalidOperationException(Loc.Instance["branch_not_selected"]);
        var result = await api.RegisterAsync(new RegisterSmsGatewayRequest(branchId, auth.DeviceId,
            auth.DeviceName, "store", sim.Slot, sim.Operator, sim.SubscriptionId, phoneLabel, token, consent));
        if (!string.IsNullOrWhiteSpace(result.HostToken))
            await SecureStorage.SetAsync(TokenKey(sim.Slot), result.HostToken);
        Preferences.Set(DeviceKey(sim.Slot), result.Device.Id);
        Preferences.Set(SubscriptionKey(sim.Slot), sim.SubscriptionId);
        Preferences.Set(ConsentKey(sim.Slot), true);
        AddSlot(sim.Slot);
        await RestartAsync();
        return result;
    }

    public async Task<IReadOnlyList<SmsGatewayHostStateDto>> GetStatesAsync()
    {
        var states = new List<SmsGatewayHostStateDto>();
        foreach (var registration in await RegistrationsAsync(includeRevoked: true))
        {
            try
            {
                states.Add(await api.GetHostStateAsync(new SmsGatewayHostStateRequest(
                    auth.DeviceId, registration.SimSlot, registration.Token)));
            }
            catch (Refit.ApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                RemoveSlot(registration.SimSlot);
            }
        }
        return states;
    }

    public async Task<UpdateSmsGatewayConsentResult> UpdateConsentAsync(
        long deviceId,
        int simSlot,
        SmsGatewayConsentScope scope,
        bool consentGranted)
    {
        var token = await HostTokenAsync(simSlot);
        return await api.UpdateConsentAsync(deviceId,
            new UpdateSmsGatewayConsentRequest(auth.DeviceId, simSlot, token, scope, consentGranted));
    }

    public async Task SetPauseAsync(long deviceId, int simSlot, bool paused)
    {
        var token = await HostTokenAsync(simSlot);
        await api.SetPauseAsync(deviceId, new SetSmsGatewayPauseRequest(auth.DeviceId, simSlot, token, paused));
        await RestartAsync();
    }

    public async Task SendTestAsync(int simSlot, string phone)
    {
        var token = await HostTokenAsync(simSlot);
        await api.SendTestAsync(new SendSmsGatewayTestRequest(auth.DeviceId, simSlot, token, phone));
    }

    public async Task SetConsentAsync(long deviceId, int simSlot, bool consented)
    {
        var token = await HostTokenAsync(simSlot);
        await api.SetConsentAsync(deviceId, new SetSmsGatewayConsentRequest(auth.DeviceId, simSlot, token, consented));
        Preferences.Set(ConsentKey(simSlot), consented);
        await RestartAsync();
    }

    private async Task RestartAsync()
    {
        await StopAsync();
        await StartAsync();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        _hub = new HubConnectionBuilder()
            .WithUrl($"{session.ServerUrl}/hubs/sms-gateway", options => options.AccessTokenProvider = () => Task.FromResult(session.AccessToken))
            .WithAutomaticReconnect()
            .Build();
        _hub.On<long>("SmsJobAvailable", _ => RequestPoll());
        _hub.Reconnected += async _ =>
        {
            await SubscribeAllAsync(cancellationToken);
            RequestPoll();
        };
        try
        {
            await _hub.StartAsync(cancellationToken);
            await SubscribeAllAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(exception);
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            // Bitta aylanishdagi xato butun hostni o'ldirmasligi kerak: aks holda SMS
            // yuborish jimgina to'xtaydi va faqat ilovani qayta ishga tushirish tiklaydi.
            var idle = true;
            try
            {
                var registrations = await RegistrationsAsync();
                idle = await PollAsync(registrations, cancellationToken) == 0;
                foreach (var registration in registrations)
                    await api.HeartbeatAsync(new SmsGatewayHeartbeatRequest(auth.DeviceId,
                        registration.SimSlot, registration.Token, null), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine(exception);
            }
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(idle ? 60 : 6), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task SubscribeAllAsync(CancellationToken cancellationToken)
    {
        if (_hub is null)
            return;
        foreach (var registration in await RegistrationsAsync())
            await _hub.InvokeAsync("Subscribe", auth.DeviceId, registration.SimSlot, registration.Token, cancellationToken);
    }

    private void RequestPoll()
    {
        Interlocked.Exchange(ref _pollAgain, 1);
        _ = PollRequestedAsync();
    }

    private async Task PollRequestedAsync()
    {
        try
        {
            if (_lifetime is not null)
                await PollAsync(await RegistrationsAsync(), _lifetime.Token);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(exception);
        }
    }

    /// Yuborilgan ish soni qaytariladi: bir narsa ketgan bo'lsa qurilma tezlik
    /// chegarasiga tushadi va navbatdagi ish tayinlanmay qoladi, shuning uchun
    /// chaqiruvchi 60 soniyani kutmasdan qisqa vaqtdan keyin qayta so'raydi.
    private async Task<int> PollAsync(
        IReadOnlyList<SmsGatewayRegistration> registrations,
        CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _polling, 1) != 0)
        {
            Interlocked.Exchange(ref _pollAgain, 1);
            return 0;
        }
        var handled = 0;
        try
        {
            do
            {
                Interlocked.Exchange(ref _pollAgain, 0);
                foreach (var registration in registrations)
                {
                    var jobs = await api.GetAssignedAsync(new SmsGatewayHostRequest(auth.DeviceId,
                        registration.SimSlot, registration.Token), cancellationToken);
                    foreach (var job in jobs)
                    {
                        await SendAsync(job, registration, cancellationToken);
                        handled++;
                    }
                }
            }
            while (Interlocked.Exchange(ref _pollAgain, 0) != 0);
        }
        finally
        {
            Volatile.Write(ref _polling, 0);
        }
        return handled;
    }

    private async Task SendAsync(
        AssignedSmsGatewayJobDto job,
        SmsGatewayRegistration registration,
        CancellationToken cancellationToken)
    {
        await _sendGate.WaitAsync(cancellationToken);
        var sentReported = false;
        try
        {
            var lease = new SmsGatewayLeaseRequest(auth.DeviceId, registration.SimSlot,
                registration.Token, job.LeaseToken);
            if (job.Simulate)
            {
                await api.MarkSimulatedAsync(job.Id, lease, cancellationToken);
                return;
            }
            await EnforceThrottleAsync(job, registration.SimSlot, cancellationToken);
            await platform.SendAsync(registration.SubscriptionId, job.Phone, job.Text, job.Id,
                async () =>
                {
                    await api.MarkSentAsync(job.Id, lease, cancellationToken);
                    sentReported = true;
                    RecordSent(registration.SimSlot, job.SegmentCount);
                },
                () => api.MarkDeliveredAsync(job.Id, lease, cancellationToken), cancellationToken);
        }
        catch (Exception exception)
        {
            if (sentReported)
            {
                System.Diagnostics.Debug.WriteLine(exception);
                return;
            }
            try
            {
                await api.MarkFailedAsync(job.Id, new SmsGatewayFailureRequest(auth.DeviceId,
                    registration.SimSlot, registration.Token, job.LeaseToken,
                    exception.GetType().Name, exception.Message), cancellationToken);
            }
            catch (Exception reportException)
            {
                System.Diagnostics.Debug.WriteLine(reportException);
            }
        }
        finally
        {
            _sendGate.Release();
        }
    }

    private static async Task EnforceThrottleAsync(
        AssignedSmsGatewayJobDto job,
        int simSlot,
        CancellationToken cancellationToken)
    {
        var timestamps = SentTimestamps(simSlot);
        var cutoff = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();
        timestamps.RemoveAll(x => x <= cutoff);
        if (timestamps.Count + job.SegmentCount > job.MaxPerHour)
            throw new InvalidOperationException(Loc.Instance["sms_gateway_hour_limit"]);
        if (timestamps.Count > 0)
        {
            var waitUntil = DateTimeOffset.FromUnixTimeSeconds(timestamps[^1]).AddSeconds(job.MinIntervalSeconds);
            var delay = waitUntil - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, cancellationToken);
        }
    }

    private static void RecordSent(int simSlot, int segments)
    {
        var timestamps = SentTimestamps(simSlot);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        for (var index = 0; index < segments; index++)
            timestamps.Add(now);
        Preferences.Set(TimestampsKey(simSlot), string.Join(',', timestamps.TakeLast(300)));
    }

    private static List<long> SentTimestamps(int simSlot) => Preferences.Get(TimestampsKey(simSlot), string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(x => long.TryParse(x, out var value) ? value : 0)
        .Where(x => x > 0)
        .ToList();

    private async Task<IReadOnlyList<SmsGatewayRegistration>> RegistrationsAsync(bool includeRevoked = false)
    {
        var registrations = new List<SmsGatewayRegistration>();
        foreach (var slot in Slots())
        {
            if (!includeRevoked && !Preferences.Get(ConsentKey(slot), false))
                continue;
            var token = await SecureStorage.GetAsync(TokenKey(slot));
            var subscriptionId = Preferences.Get(SubscriptionKey(slot), string.Empty);
            if (!string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(subscriptionId))
                registrations.Add(new SmsGatewayRegistration(slot, subscriptionId, token));
        }
        return registrations;
    }

    private static async Task<string> HostTokenAsync(int simSlot) => await SecureStorage.GetAsync(TokenKey(simSlot))
        ?? throw new InvalidOperationException(Loc.Instance["sms_gateway_register_first"]);

    private static IReadOnlyList<int> Slots() => Preferences.Get("sms_gateway_slots", string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(x => int.TryParse(x, out var slot) ? slot : -1)
        .Where(x => x >= 0)
        .Distinct()
        .Order()
        .ToList();

    private static void AddSlot(int simSlot)
    {
        var slots = Slots().Append(simSlot).Distinct().Order();
        Preferences.Set("sms_gateway_slots", string.Join(',', slots));
    }

    private static void RemoveSlot(int simSlot)
    {
        Preferences.Set("sms_gateway_slots", string.Join(',', Slots().Where(x => x != simSlot)));
        Preferences.Remove(DeviceKey(simSlot));
        Preferences.Remove(SubscriptionKey(simSlot));
        Preferences.Remove(ConsentKey(simSlot));
        SecureStorage.Remove(TokenKey(simSlot));
    }

    private static string TokenKey(int simSlot) => $"sms_gateway_host_token_{simSlot}";
    private static string DeviceKey(int simSlot) => $"sms_gateway_device_id_{simSlot}";
    private static string SubscriptionKey(int simSlot) => $"sms_gateway_subscription_id_{simSlot}";
    private static string ConsentKey(int simSlot) => $"sms_gateway_consented_{simSlot}";
    private static string TimestampsKey(int simSlot) => $"sms_gateway_sent_timestamps_{simSlot}";

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _sendGate.Dispose();
    }
}

public sealed record SmsGatewayRegistration(int SimSlot, string SubscriptionId, string Token);
