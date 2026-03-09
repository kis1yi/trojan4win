using System;
using System.Threading;
using trojan4win.Services;
using Xunit;

namespace trojan4win.Tests;

public sealed class TrafficMonitorTests : IDisposable
{
    private readonly TrafficMonitor _monitor = new();

    public void Dispose() => _monitor.Dispose();

    // ── Start state ───────────────────────────────────────────────────────────

    [Fact]
    public void AfterStart_AllCountersAreZero()
    {
        _monitor.Start();

        Assert.Equal(0L, _monitor.SessionBytesUp);
        Assert.Equal(0L, _monitor.SessionBytesDown);
        Assert.Equal(0L, _monitor.SpeedUp);
        Assert.Equal(0L, _monitor.SpeedDown);
    }

    [Fact]
    public void AfterStart_SessionDurationIsNonNegative()
    {
        _monitor.Start();
        Assert.True(_monitor.SessionDuration >= TimeSpan.Zero);
    }

    // ── Stop state ────────────────────────────────────────────────────────────

    [Fact]
    public void AfterStop_SpeedsAreZero()
    {
        _monitor.Start();
        _monitor.Stop();

        Assert.Equal(0L, _monitor.SpeedUp);
        Assert.Equal(0L, _monitor.SpeedDown);
    }

    [Fact]
    public void Stop_WithoutStart_DoesNotThrow()
    {
        var ex = Record.Exception(() => _monitor.Stop());
        Assert.Null(ex);
    }

    [Fact]
    public void Stop_CalledTwice_IsIdempotentAndDoesNotThrow()
    {
        _monitor.Start();
        _monitor.Stop();

        var ex = Record.Exception(() => _monitor.Stop());
        Assert.Null(ex);
    }

    [Fact]
    public void Stop_FiresUpdatedEvent()
    {
        bool fired = false;
        _monitor.Updated += () => fired = true;

        _monitor.Start();
        _monitor.Stop();

        Assert.True(fired, "Stop() must fire the Updated event (CR-08/CR-09)");
    }

    // ── Dispose delegates to Stop (CR-18) ────────────────────────────────────

    [Fact]
    public void Dispose_SpeedsAreZeroAfterwards()
    {
        _monitor.Start();
        _monitor.Dispose();

        Assert.Equal(0L, _monitor.SpeedUp);
        Assert.Equal(0L, _monitor.SpeedDown);
    }

    [Fact]
    public void Dispose_FiresUpdatedEvent()
    {
        // CR-18: Dispose() delegates to Stop(), which zeroes speeds and fires Updated
        bool fired = false;
        _monitor.Updated += () => fired = true;

        _monitor.Start();
        _monitor.Dispose(); // should call Stop() internally

        Assert.True(fired, "Dispose() must fire Updated via Stop() (CR-18)");
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        _monitor.Start();
        _monitor.Dispose();

        var ex = Record.Exception(() => _monitor.Dispose());
        Assert.Null(ex);
    }

    // ── Start → Stop → Start cycle ────────────────────────────────────────────

    [Fact]
    public void Restart_ResetsCounters()
    {
        _monitor.Start();
        _monitor.Stop();
        _monitor.Start(); // second Start must reset everything

        Assert.Equal(0L, _monitor.SessionBytesUp);
        Assert.Equal(0L, _monitor.SessionBytesDown);
        Assert.Equal(0L, _monitor.SpeedUp);
        Assert.Equal(0L, _monitor.SpeedDown);
    }
}
