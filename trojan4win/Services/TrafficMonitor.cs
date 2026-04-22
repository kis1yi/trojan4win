using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

namespace trojan4win.Services;

// Per-process traffic monitor for trojan-go.
//
// We do NOT sum per-interface bytes — that would count every byte the OS sends,
// regardless of whether the user's traffic was actually proxified. Instead we:
//   1) enumerate TCP connections owned by the trojan-go PID (IPv4 + IPv6),
//   2) skip loopback remotes (those are the SOCKS ingress from local apps —
//      counting them would double the upstream bytes),
//   3) read cumulative DataBytesIn/DataBytesOut via TCP eStats per connection,
//   4) accumulate per-connection deltas into the session totals (so closed
//      connections keep contributing their final bytes).
public class TrafficMonitor : IDisposable
{
    private Timer? _timer;
    private volatile bool _stopping;
    private int _pid;

    private long _sessionBytesUp;
    private long _sessionBytesDown;
    private long _speedUp;
    private long _speedDown;
    private readonly Stopwatch _sessionWatch = new();

    // Cumulative bytes seen on each live connection. We sum (current − previous)
    // into the session totals each tick, so when a connection disappears its
    // last accumulated contribution stays in the totals.
    private readonly Dictionary<ConnKey, (ulong Up, ulong Down)> _connBytes = new();

    public long SessionBytesUp => Interlocked.Read(ref _sessionBytesUp);
    public long SessionBytesDown => Interlocked.Read(ref _sessionBytesDown);
    public long SpeedUp => Interlocked.Read(ref _speedUp);
    public long SpeedDown => Interlocked.Read(ref _speedDown);
    public TimeSpan SessionDuration => _sessionWatch.Elapsed;

    public event Action? Updated;

    public void Start(int pid)
    {
        _stopping = false;
        _pid = pid;
        Interlocked.Exchange(ref _sessionBytesUp, 0);
        Interlocked.Exchange(ref _sessionBytesDown, 0);
        Interlocked.Exchange(ref _speedUp, 0);
        Interlocked.Exchange(ref _speedDown, 0);
        _connBytes.Clear();

        _sessionWatch.Restart();
        _timer = new Timer(Tick, null, 1000, 1000);
    }

    public void Stop()
    {
        _stopping = true;
        _timer?.Dispose();
        _timer = null;
        _sessionWatch.Stop();
        Interlocked.Exchange(ref _speedUp, 0);
        Interlocked.Exchange(ref _speedDown, 0);
        _connBytes.Clear();
        Updated?.Invoke();
    }

    private void Tick(object? state)
    {
        if (_stopping || _pid <= 0) return;
        try
        {
            long deltaUp = 0;
            long deltaDown = 0;
            var seen = new HashSet<ConnKey>();

            CollectV4(_pid, seen, ref deltaUp, ref deltaDown);
            CollectV6(_pid, seen, ref deltaUp, ref deltaDown);

            // Drop entries for connections that no longer exist; their final
            // bytes are already baked into _sessionBytes* via prior deltas.
            if (_connBytes.Count != seen.Count)
            {
                var stale = new List<ConnKey>();
                foreach (var k in _connBytes.Keys)
                    if (!seen.Contains(k)) stale.Add(k);
                foreach (var k in stale) _connBytes.Remove(k);
            }

            if (deltaUp < 0) deltaUp = 0;
            if (deltaDown < 0) deltaDown = 0;

            Interlocked.Add(ref _sessionBytesUp, deltaUp);
            Interlocked.Add(ref _sessionBytesDown, deltaDown);
            Interlocked.Exchange(ref _speedUp, deltaUp);
            Interlocked.Exchange(ref _speedDown, deltaDown);

            if (!_stopping) Updated?.Invoke();
        }
        catch
        {
            // ignored — transient enumeration failures must not crash the timer
        }
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    // ── Connection enumeration ────────────────────────────────────────────────

    private void CollectV4(int pid, HashSet<ConnKey> seen, ref long up, ref long down)
    {
        var rows = GetTcpV4Rows();
        foreach (var r in rows)
        {
            if ((int)r.dwOwningPid != pid) continue;
            var remote = new IPAddress(BitConverter.GetBytes(r.dwRemoteAddr));
            if (IsLoopbackOrAny(remote)) continue;

            var key = new ConnKey(
                AddressFamily.InterNetwork,
                BitConverter.GetBytes(r.dwLocalAddr), NtoHs((ushort)r.dwLocalPort),
                BitConverter.GetBytes(r.dwRemoteAddr), NtoHs((ushort)r.dwRemotePort));
            seen.Add(key);

            // Build the MIB_TCPROW key eStats expects (state + 4 endpoint DWORDs).
            var keyRow = new MIB_TCPROW
            {
                dwState = r.dwState,
                dwLocalAddr = r.dwLocalAddr,
                dwLocalPort = r.dwLocalPort,
                dwRemoteAddr = r.dwRemoteAddr,
                dwRemotePort = r.dwRemotePort
            };
            if (TryReadV4(keyRow, out var bytesOut, out var bytesIn))
                Accumulate(key, bytesOut, bytesIn, ref up, ref down);
        }
    }

    private void CollectV6(int pid, HashSet<ConnKey> seen, ref long up, ref long down)
    {
        var rows = GetTcpV6Rows();
        foreach (var r in rows)
        {
            if ((int)r.dwOwningPid != pid) continue;
            var remote = new IPAddress(r.ucRemoteAddr);
            if (IsLoopbackOrAny(remote)) continue;

            var key = new ConnKey(
                AddressFamily.InterNetworkV6,
                r.ucLocalAddr, NtoHs((ushort)r.dwLocalPort),
                r.ucRemoteAddr, NtoHs((ushort)r.dwRemotePort));
            seen.Add(key);

            var keyRow = new MIB_TCP6ROW
            {
                State = r.dwState,
                LocalAddr = r.ucLocalAddr,
                dwLocalScopeId = r.dwLocalScopeId,
                dwLocalPort = r.dwLocalPort,
                RemoteAddr = r.ucRemoteAddr,
                dwRemoteScopeId = r.dwRemoteScopeId,
                dwRemotePort = r.dwRemotePort
            };
            if (TryReadV6(keyRow, out var bytesOut, out var bytesIn))
                Accumulate(key, bytesOut, bytesIn, ref up, ref down);
        }
    }

    private void Accumulate(ConnKey key, ulong bytesOut, ulong bytesIn, ref long up, ref long down)
    {
        if (_connBytes.TryGetValue(key, out var prev))
        {
            if (bytesOut > prev.Up) up += (long)(bytesOut - prev.Up);
            if (bytesIn > prev.Down) down += (long)(bytesIn - prev.Down);
        }
        else
        {
            // First observation: bytes accumulated before we saw the connection
            // count toward the session — eStats may have been enabled lazily, so
            // the first read can already be non-zero.
            up += (long)bytesOut;
            down += (long)bytesIn;
        }
        _connBytes[key] = (bytesOut, bytesIn);
    }

    private static bool IsLoopbackOrAny(IPAddress addr) =>
        IPAddress.IsLoopback(addr) || addr.Equals(IPAddress.Any) || addr.Equals(IPAddress.IPv6Any);

    private static ushort NtoHs(ushort port) =>
        (ushort)(((port & 0xFF) << 8) | ((port >> 8) & 0xFF));

    // ── eStats reads ──────────────────────────────────────────────────────────

    private static bool TryReadV4(MIB_TCPROW row, out ulong bytesOut, out ulong bytesIn)
    {
        bytesOut = 0;
        bytesIn = 0;
        var rw = new TCP_ESTATS_DATA_RW_v0 { EnableCollection = 1 };
        var rwSize = (uint)Marshal.SizeOf<TCP_ESTATS_DATA_RW_v0>();
        var rwPtr = Marshal.AllocHGlobal((int)rwSize);
        try
        {
            Marshal.StructureToPtr(rw, rwPtr, false);
            // Best-effort enable; ignore failure (already enabled or no perms).
            _ = SetPerTcpConnectionEStats(ref row, TcpConnectionEstatsData, rwPtr, 0, rwSize, 0);

            var rodSize = (uint)Marshal.SizeOf<TCP_ESTATS_DATA_ROD_v0>();
            var rodPtr = Marshal.AllocHGlobal((int)rodSize);
            try
            {
                var ret = GetPerTcpConnectionEStats(
                    ref row, TcpConnectionEstatsData,
                    IntPtr.Zero, 0, 0,
                    IntPtr.Zero, 0, 0,
                    rodPtr, 0, rodSize);
                if (ret != 0) return false;
                var rod = Marshal.PtrToStructure<TCP_ESTATS_DATA_ROD_v0>(rodPtr);
                bytesOut = rod.DataBytesOut;
                bytesIn = rod.DataBytesIn;
                return true;
            }
            finally { Marshal.FreeHGlobal(rodPtr); }
        }
        finally { Marshal.FreeHGlobal(rwPtr); }
    }

    private static bool TryReadV6(MIB_TCP6ROW row, out ulong bytesOut, out ulong bytesIn)
    {
        bytesOut = 0;
        bytesIn = 0;
        var rw = new TCP_ESTATS_DATA_RW_v0 { EnableCollection = 1 };
        var rwSize = (uint)Marshal.SizeOf<TCP_ESTATS_DATA_RW_v0>();
        var rwPtr = Marshal.AllocHGlobal((int)rwSize);
        try
        {
            Marshal.StructureToPtr(rw, rwPtr, false);
            _ = SetPerTcp6ConnectionEStats(ref row, TcpConnectionEstatsData, rwPtr, 0, rwSize, 0);

            var rodSize = (uint)Marshal.SizeOf<TCP_ESTATS_DATA_ROD_v0>();
            var rodPtr = Marshal.AllocHGlobal((int)rodSize);
            try
            {
                var ret = GetPerTcp6ConnectionEStats(
                    ref row, TcpConnectionEstatsData,
                    IntPtr.Zero, 0, 0,
                    IntPtr.Zero, 0, 0,
                    rodPtr, 0, rodSize);
                if (ret != 0) return false;
                var rod = Marshal.PtrToStructure<TCP_ESTATS_DATA_ROD_v0>(rodPtr);
                bytesOut = rod.DataBytesOut;
                bytesIn = rod.DataBytesIn;
                return true;
            }
            finally { Marshal.FreeHGlobal(rodPtr); }
        }
        finally { Marshal.FreeHGlobal(rwPtr); }
    }

    // ── TCP table enumeration ─────────────────────────────────────────────────

    private static List<MIB_TCPROW_OWNER_PID> GetTcpV4Rows()
    {
        var result = new List<MIB_TCPROW_OWNER_PID>();
        uint size = 0;
        GetExtendedTcpTable(IntPtr.Zero, ref size, false, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
        if (size == 0) return result;
        var buf = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetExtendedTcpTable(buf, ref size, false, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0) != 0)
                return result;
            int rowCount = Marshal.ReadInt32(buf);
            int rowSize = Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();
            // MIB_TCPTABLE_OWNER_PID is `DWORD dwNumEntries; MIB_TCPROW_OWNER_PID table[]`.
            // The first row begins immediately after the 4-byte count; the row
            // itself contains only DWORDs so no extra padding is inserted.
            IntPtr p = IntPtr.Add(buf, 4);
            for (int i = 0; i < rowCount; i++)
            {
                var row = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(p);
                result.Add(row);
                p = IntPtr.Add(p, rowSize);
            }
        }
        finally { Marshal.FreeHGlobal(buf); }
        return result;
    }

    private static List<MIB_TCP6ROW_OWNER_PID> GetTcpV6Rows()
    {
        var result = new List<MIB_TCP6ROW_OWNER_PID>();
        uint size = 0;
        GetExtendedTcpTable(IntPtr.Zero, ref size, false, AF_INET6, TCP_TABLE_OWNER_PID_ALL, 0);
        if (size == 0) return result;
        var buf = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetExtendedTcpTable(buf, ref size, false, AF_INET6, TCP_TABLE_OWNER_PID_ALL, 0) != 0)
                return result;
            int rowCount = Marshal.ReadInt32(buf);
            int rowSize = Marshal.SizeOf<MIB_TCP6ROW_OWNER_PID>();
            IntPtr p = IntPtr.Add(buf, 4);
            for (int i = 0; i < rowCount; i++)
            {
                var row = Marshal.PtrToStructure<MIB_TCP6ROW_OWNER_PID>(p);
                result.Add(row);
                p = IntPtr.Add(p, rowSize);
            }
        }
        finally { Marshal.FreeHGlobal(buf); }
        return result;
    }

    // ── Connection key ────────────────────────────────────────────────────────

    private readonly struct ConnKey : IEquatable<ConnKey>
    {
        private readonly AddressFamily _family;
        private readonly byte[] _local;
        private readonly ushort _localPort;
        private readonly byte[] _remote;
        private readonly ushort _remotePort;

        public ConnKey(AddressFamily family, byte[] local, ushort localPort, byte[] remote, ushort remotePort)
        {
            _family = family;
            _local = (byte[])local.Clone();
            _localPort = localPort;
            _remote = (byte[])remote.Clone();
            _remotePort = remotePort;
        }

        public bool Equals(ConnKey other)
        {
            if (_family != other._family || _localPort != other._localPort || _remotePort != other._remotePort) return false;
            if (_local.Length != other._local.Length || _remote.Length != other._remote.Length) return false;
            for (int i = 0; i < _local.Length; i++) if (_local[i] != other._local[i]) return false;
            for (int i = 0; i < _remote.Length; i++) if (_remote[i] != other._remote[i]) return false;
            return true;
        }

        public override bool Equals(object? obj) => obj is ConnKey k && Equals(k);

        public override int GetHashCode()
        {
            var h = new HashCode();
            h.Add((int)_family);
            h.Add(_localPort);
            h.Add(_remotePort);
            foreach (var b in _local) h.Add(b);
            foreach (var b in _remote) h.Add(b);
            return h.ToHashCode();
        }
    }

    // ── P/Invoke definitions ─────────────────────────────────────────────────

    private const uint AF_INET = 2;
    private const uint AF_INET6 = 23;
    private const uint TCP_TABLE_OWNER_PID_ALL = 5;
    private const int TcpConnectionEstatsData = 1;

    [DllImport("iphlpapi.dll", SetLastError = false)]
    private static extern uint GetExtendedTcpTable(
        IntPtr pTcpTable, ref uint pdwSize, bool bOrder,
        uint ulAf, uint TableClass, uint Reserved);

    [DllImport("iphlpapi.dll")]
    private static extern uint SetPerTcpConnectionEStats(
        ref MIB_TCPROW Row, int EstatsType,
        IntPtr Rw, uint RwVersion, uint RwSize, uint Offset);

    [DllImport("iphlpapi.dll")]
    private static extern uint GetPerTcpConnectionEStats(
        ref MIB_TCPROW Row, int EstatsType,
        IntPtr Rw, uint RwVersion, uint RwSize,
        IntPtr Ros, uint RosVersion, uint RosSize,
        IntPtr Rod, uint RodVersion, uint RodSize);

    [DllImport("iphlpapi.dll")]
    private static extern uint SetPerTcp6ConnectionEStats(
        ref MIB_TCP6ROW Row, int EstatsType,
        IntPtr Rw, uint RwVersion, uint RwSize, uint Offset);

    [DllImport("iphlpapi.dll")]
    private static extern uint GetPerTcp6ConnectionEStats(
        ref MIB_TCP6ROW Row, int EstatsType,
        IntPtr Rw, uint RwVersion, uint RwSize,
        IntPtr Ros, uint RosVersion, uint RosSize,
        IntPtr Rod, uint RodVersion, uint RodSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPROW_OWNER_PID
    {
        public uint dwState;
        public uint dwLocalAddr;
        public uint dwLocalPort;
        public uint dwRemoteAddr;
        public uint dwRemotePort;
        public uint dwOwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCP6ROW_OWNER_PID
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] ucLocalAddr;
        public uint dwLocalScopeId;
        public uint dwLocalPort;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] ucRemoteAddr;
        public uint dwRemoteScopeId;
        public uint dwRemotePort;
        public uint dwState;
        public uint dwOwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPROW
    {
        public uint dwState;
        public uint dwLocalAddr;
        public uint dwLocalPort;
        public uint dwRemoteAddr;
        public uint dwRemotePort;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCP6ROW
    {
        public uint State;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] LocalAddr;
        public uint dwLocalScopeId;
        public uint dwLocalPort;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] RemoteAddr;
        public uint dwRemoteScopeId;
        public uint dwRemotePort;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TCP_ESTATS_DATA_RW_v0
    {
        public byte EnableCollection;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TCP_ESTATS_DATA_ROD_v0
    {
        // Field order must match the Win32 TCP_ESTATS_DATA_ROD_v0 layout exactly.
        // DataSegsOut sits between DataBytesOut and DataBytesIn — getting this
        // wrong silently returns segment counts instead of byte counts.
        public ulong DataBytesOut;
        public ulong DataSegsOut;
        public ulong DataBytesIn;
        public ulong DataSegsIn;
        public ulong SegsOut;
        public ulong SegsIn;
        public uint SoftErrors;
        public uint SoftErrorReason;
        public uint SndUna;
        public uint SndNxt;
        public uint SndMax;
        public ulong ThruBytesAcked;
        public uint RcvNxt;
        public ulong ThruBytesReceived;
    }
}