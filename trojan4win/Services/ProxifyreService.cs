using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace trojan4win.Services;

public class ProxifyreService : IDisposable
{
    private Process? _proxyProcess;
    private readonly StringBuilder _logBuffer = new();
    private readonly object _logLock = new();

    public event Action<string>? LogReceived;
    public event Action? ProcessExited;

    public bool IsRunning => _proxyProcess is { HasExited: false };

    public string GetLogs()
    {
        lock (_logLock)
            return _logBuffer.ToString();
    }

    public void ClearLogs()
    {
        lock (_logLock)
            _logBuffer.Clear();
    }

    private string GetProxifyreDir()
    {
        return Path.Combine(AppContext.BaseDirectory, "Tools", "proxifyre");
    }

    public async Task StartAsync(int socksPort, string localAddr, IReadOnlyList<string> supportedProtocols, string logLevel, IReadOnlyList<string> excludedProcesses, CancellationToken ct = default)
    {
        if (IsRunning) return;

        var proxyDir = GetProxifyreDir();
        var proxyPath = Path.Combine(proxyDir, "ProxiFyre.exe");
        if (!File.Exists(proxyPath))
            throw new FileNotFoundException("ProxiFyre.exe not found. Place it in Tools/proxifyre/.", proxyPath);

        WriteProxifyreConfig(proxyDir, socksPort, localAddr, supportedProtocols, logLevel, excludedProcesses);

        _proxyProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = proxyPath,
                Arguments = "",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = proxyDir
            },
            EnableRaisingEvents = true
        };

        _proxyProcess.OutputDataReceived += OnDataReceived;
        _proxyProcess.ErrorDataReceived += OnDataReceived;
        _proxyProcess.Exited += (_, _) => ProcessExited?.Invoke();

        try
        {
            _proxyProcess.Start();
            _proxyProcess.BeginOutputReadLine();
            _proxyProcess.BeginErrorReadLine();
            await Task.Delay(500, ct);
        }
        catch
        {
            // CR-04: prevent orphan process if cancellation or Start() throws
            Stop();
            throw;
        }
    }

    public void Stop()
    {
        try
        {
            if (_proxyProcess is { HasExited: false })
            {
                _proxyProcess.Kill(true);
                _proxyProcess.WaitForExit(3000);
            }
        }
        catch
        {
            // ignored
        }
        finally
        {
            _proxyProcess?.Dispose();
            _proxyProcess = null;
        }
    }

    private void OnDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data == null) return;
        var line = $"[{DateTime.Now:HH:mm:ss}] {e.Data}";
        lock (_logLock)
        {
            _logBuffer.AppendLine(line);
            if (_logBuffer.Length > 512 * 1024)
                _logBuffer.Remove(0, _logBuffer.Length / 2);
        }

        LogReceived?.Invoke(line);
    }

    internal void WriteProxifyreConfig(string proxyDir, int socksPort, string localAddr, IReadOnlyList<string> supportedProtocols, string logLevel, IReadOnlyList<string> excludedProcesses)
    {
        var configPath = Path.Combine(proxyDir, "app-config.json");

        var excludes = new List<string> { "trojan.exe" };
        foreach (var p in excludedProcesses)
        {
            var name = p.Trim();
            if (!string.IsNullOrEmpty(name) && !excludes.Contains(name, StringComparer.OrdinalIgnoreCase))
                excludes.Add(name);
        }

        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"logLevel\": \"{EscapeJson(logLevel)}\",");
        sb.AppendLine("  \"proxies\": [");
        sb.AppendLine("    {");
        sb.AppendLine("      \"appNames\": [\"\"],");
        sb.AppendLine("      \"username\": \"\",");
        sb.AppendLine("      \"password\": \"\",");
        sb.AppendLine($"      \"socks5ProxyEndpoint\": \"{EscapeJson(localAddr)}:{socksPort}\",");

        sb.Append("      \"supportedProtocols\": [");
        for (int i = 0; i < supportedProtocols.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append($"\"{EscapeJson(supportedProtocols[i])}\"");
        }
        sb.AppendLine("]");

        sb.AppendLine("    }");
        sb.AppendLine("  ],");

        sb.Append("  \"excludes\": [");
        for (int i = 0; i < excludes.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append($"\"{EscapeJson(excludes[i])}\"");
        }
        sb.AppendLine("]");

        sb.AppendLine("}");

        File.WriteAllText(configPath, sb.ToString(), System.Text.Encoding.UTF8);
    }

    internal static string EscapeJson(string s)
    {
        // CR-02: use JsonSerializer for spec-compliant escaping — handles backslashes,
        // quotes, control characters (\n, \r, \t, \0, U+0000-U+001F) and Unicode correctly.
        // Serialize produces "value" with surrounding quotes; [1..^1] strips them.
        return JsonSerializer.Serialize(s)[1..^1];
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
