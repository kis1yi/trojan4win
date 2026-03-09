using System.Threading;
using System.Threading.Tasks;
using trojan4win.Services;
using Xunit;

namespace trojan4win.Tests;

public class PingServiceTests
{
    // NOTE: MeasurePingAsync_Localhost relies on ICMP access to 127.0.0.1.
    // On Windows the loopback interface is never blocked by the OS firewall, so this
    // should be stable on any developer machine or standard CI agent.
    // If it fails in a heavily restricted environment, the test can be skipped with
    // [Trait("Category", "RequiresNetwork")] and a custom xunit filter.

    [Fact]
    public async Task MeasurePingAsync_Localhost_ReturnsNonNegative()
    {
        var result = await PingService.MeasurePingAsync("127.0.0.1");
        Assert.True(result >= 0, $"Expected a successful ping to 127.0.0.1, got {result}");
    }

    [Fact]
    public async Task MeasurePingAsync_InvalidHost_ReturnsNegativeOne()
    {
        // A hostname that cannot resolve; PingService must catch the exception and return -1
        var result = await PingService.MeasurePingAsync("invalid.host.does.not.exist.xyzabc123");
        Assert.Equal(-1, result);
    }

    [Fact]
    public async Task MeasurePingAsync_AlreadyCancelledToken_ReturnsNegativeOne()
    {
        // CR-10: cancellation token is forwarded to SendPingAsync;
        // a pre-cancelled token must not propagate an exception to the caller
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await PingService.MeasurePingAsync("127.0.0.1", cts.Token);
        Assert.Equal(-1, result);
    }
}
