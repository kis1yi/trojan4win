using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using trojan4win.Models;

namespace trojan4win.Services;

public sealed class SubscriptionScheduler : IDisposable
{
    private readonly SubscriptionService _service;
    private readonly Func<IReadOnlyList<SubscriptionConfig>> _subscriptions;
    private readonly Timer _timer;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SubscriptionScheduler(
        SubscriptionService service,
        Func<IReadOnlyList<SubscriptionConfig>> subscriptions)
    {
        _service = service;
        _subscriptions = subscriptions;
        _timer = new Timer(OnTimer, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }

    public event Action<SubscriptionConfig, Exception?>? RefreshCompleted;

    public Task RefreshNowAsync(SubscriptionConfig subscription, CancellationToken token = default) =>
        RefreshOneAsync(subscription, token);

    private async void OnTimer(object? _)
    {
        if (!await _gate.WaitAsync(0)) return;
        try
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var subscription in _subscriptions().ToList())
            {
                var due = subscription.LastUpdatedUtc is null ||
                    subscription.LastUpdatedUtc.Value.AddHours(subscription.UpdateIntervalHours) <= now;
                if (due) await RefreshOneAsync(subscription, CancellationToken.None);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task RefreshOneAsync(SubscriptionConfig subscription, CancellationToken token)
    {
        Exception? failure = null;
        try
        {
            await _service.UpdateAsync(subscription, token);
            foreach (var server in subscription.ServerConfigs)
                await GeoIpService.PopulateRegionAsync(server);
        }
        catch (Exception ex) { failure = ex; }
        RefreshCompleted?.Invoke(subscription, failure);
    }

    public void Dispose()
    {
        _timer.Dispose();
        _gate.Dispose();
    }
}
