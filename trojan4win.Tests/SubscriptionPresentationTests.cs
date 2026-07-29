using System;
using System.Globalization;
using trojan4win.Converters;
using trojan4win.Models;
using trojan4win.Services;
using Xunit;

namespace trojan4win.Tests;

public sealed class SubscriptionPresentationTests
{
    [Fact]
    public void UserAgentUsesProductDisplayVersion()
    {
        var subscription = new SubscriptionConfig();

        Assert.Equal($"trojan4win/{ApplicationVersion.DisplayVersion}", subscription.UserAgent);
        Assert.DoesNotContain('+', subscription.UserAgent);
    }

    [Fact]
    public void MetadataTextIncludesAutoUpdateAndExpiresLabels()
    {
        var subscription = new SubscriptionConfig
        {
            LastUpdatedUtc = new DateTimeOffset(2026, 7, 30, 10, 20, 0, TimeSpan.Zero),
            UpdateIntervalHours = 4,
            Expire = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(),
        };

        Assert.Contains("| Auto-update · 4 h", subscription.LastUpdatedText);
        Assert.StartsWith("Expires: ", subscription.ExpireText);
    }

    [Fact]
    public void MissingPingUsesDashAndUnit()
    {
        var converter = new PingToStringConverter();

        Assert.Equal(
            "– ms",
            converter.Convert(-1, typeof(string), null, CultureInfo.InvariantCulture)
        );
    }
}
