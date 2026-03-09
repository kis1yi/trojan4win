using System;
using System.Diagnostics;
using System.Threading;

namespace trojan4win.Services;

public class TrafficMonitor : IDisposable
{
    private Timer? _timer;
    private volatile bool _stopping;     // CR-09: signals Tick() to abort when Stop() is in progress
    private long _lastBytesUp;
    private long _lastBytesDown;
    private readonly Stopwatch _sessionWatch = new();

    private long _sessionBytesUp;
    private long _sessionBytesDown;
    private long _speedUp;
    private long _speedDown;

    // CR-08: Interlocked.Read provides a full memory barrier, preventing torn reads
    // of long values written by the ThreadPool timer callback
    public long SessionBytesUp => Interlocked.Read(ref _sessionBytesUp);
    public long SessionBytesDown => Interlocked.Read(ref _sessionBytesDown);
    public long SpeedUp => Interlocked.Read(ref _speedUp);
    public long SpeedDown => Interlocked.Read(ref _speedDown);
    public TimeSpan SessionDuration => _sessionWatch.Elapsed;

    public event Action? Updated;

    public void Start()
    {
        _stopping = false;  // CR-09: clear flag before starting new session
        Interlocked.Exchange(ref _sessionBytesUp, 0);   // CR-08
        Interlocked.Exchange(ref _sessionBytesDown, 0); // CR-08
        Interlocked.Exchange(ref _speedUp, 0);          // CR-08
        Interlocked.Exchange(ref _speedDown, 0);        // CR-08
        _lastBytesUp = 0;
        _lastBytesDown = 0;

        _sessionWatch.Restart();
        _timer = new Timer(Tick, null, 1000, 1000);
    }

    public void Stop()
    {
        _stopping = true;  // CR-09: signal any in-flight Tick() to skip writing results
        _timer?.Dispose();
        _timer = null;
        _sessionWatch.Stop();
        Interlocked.Exchange(ref _speedUp, 0);   // CR-08
        Interlocked.Exchange(ref _speedDown, 0); // CR-08
        Updated?.Invoke();
    }

    private void Tick(object? state)
    {
        if (_stopping) return;  // CR-09: bail if Stop() is in progress
        try
        {
            long totalUp = 0, totalDown = 0;
            var interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
            foreach (var ni in interfaces)
            {
                if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                var stats = ni.GetIPStatistics();
                totalUp += stats.BytesSent;
                totalDown += stats.BytesReceived;
            }

            if (_lastBytesUp > 0)
            {
                var deltaUp = totalUp - _lastBytesUp;
                var deltaDown = totalDown - _lastBytesDown;
                if (deltaUp < 0) deltaUp = 0;
                if (deltaDown < 0) deltaDown = 0;
                // CR-17: a new interface appearing mid-session has no prior baseline,
                // injecting its full byte count as a one-tick spike; cap to 1 GB/tick
                const long maxDeltaPerTick = 1L * 1024 * 1024 * 1024;
                if (deltaUp > maxDeltaPerTick) deltaUp = 0;
                if (deltaDown > maxDeltaPerTick) deltaDown = 0;

                Interlocked.Add(ref _sessionBytesUp, deltaUp);    // CR-08
                Interlocked.Add(ref _sessionBytesDown, deltaDown); // CR-08
                Interlocked.Exchange(ref _speedUp, deltaUp);       // CR-08
                Interlocked.Exchange(ref _speedDown, deltaDown);   // CR-08
            }

            _lastBytesUp = totalUp;
            _lastBytesDown = totalDown;

            if (!_stopping) Updated?.Invoke();  // CR-09: skip event if Stop() raced us
        }
        catch
        {
            // ignored
        }
    }

    public void Dispose()
    {
        Stop();  // CR-18: reuse Stop() to zero speeds, fire Updated, and set _stopping flag
        GC.SuppressFinalize(this);
    }
}
