using System;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace trojan4win.Services;

public static class PingService
{
    public static async Task<int> MeasurePingAsync(string host, CancellationToken ct = default)
    {
        try
        {
            using var ping = new Ping();
            // CR-10: pass ct so cancellation interrupts the in-progress ping (.NET 8 overload)
            var reply = await ping.SendPingAsync(host, TimeSpan.FromSeconds(3), null, null, ct);
            return reply.Status == IPStatus.Success ? (int)reply.RoundtripTime : -1;
        }
        catch
        {
            return -1;
        }
    }
}
