using System;
using System.Globalization;
using System.Threading;
using trojan4win.ViewModels;
using Xunit;

namespace trojan4win.Tests;

public class MainViewModelFormatTests : IDisposable
{
    private readonly CultureInfo _originalCulture;

    public MainViewModelFormatTests()
    {
        _originalCulture = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
    }

    public void Dispose()
    {
        Thread.CurrentThread.CurrentCulture = _originalCulture;
    }

    // ── FormatBytes ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0L,                    "0 B")]
    [InlineData(1023L,                 "1023 B")]
    [InlineData(1024L,                 "1.0 KB")]
    [InlineData(1536L,                 "1.5 KB")]
    [InlineData(1024L * 1024,          "1.0 MB")]
    [InlineData(1024L * 1024 * 1024,   "1.00 GB")]
    public void FormatBytes_ReturnsExpected(long bytes, string expected)
    {
        Assert.Equal(expected, MainViewModel.FormatBytes(bytes));
    }

    // ── FormatSpeed ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0L,    "0 B/s")]
    [InlineData(1024L, "1.0 KB/s")]
    [InlineData(1536L, "1.5 KB/s")]
    public void FormatSpeed_AppendsPerSecondSuffix(long bytesPerSec, string expected)
    {
        Assert.Equal(expected, MainViewModel.FormatSpeed(bytesPerSec));
    }

    [Fact]
    public void FormatSpeed_MatchesFormatBytesWithSuffix()
    {
        const long value = 1024L * 1024;
        Assert.Equal(MainViewModel.FormatBytes(value) + "/s", MainViewModel.FormatSpeed(value));
    }

    // ── FormatDuration ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0,    "00:00")]
    [InlineData(59,   "00:59")]
    [InlineData(60,   "01:00")]
    [InlineData(3661, "01:01:01")]
    public void FormatDuration_ReturnsExpected(int seconds, string expected)
    {
        Assert.Equal(expected, MainViewModel.FormatDuration(TimeSpan.FromSeconds(seconds)));
    }

    // ── FormatTotalTime ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(30,    "0m 30s")]
    [InlineData(90,    "1m 30s")]
    [InlineData(3700,  "1h 1m")]
    [InlineData(90000, "1d 1h 0m")]
    public void FormatTotalTime_ReturnsExpected(long totalSeconds, string expected)
    {
        Assert.Equal(expected, MainViewModel.FormatTotalTime(totalSeconds));
    }
}
