using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace trojan4win.Services;

public class TrojanService : IDisposable
{
    private Process? _trojanProcess;
    private readonly StringBuilder _logBuffer = new();
    private readonly object _logLock = new();

    public event Action<string>? LogReceived;
    public event Action? ProcessExited;

    public bool IsRunning => _trojanProcess is { HasExited: false };

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

    private string GetTrojanPath()
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "Tools", "trojan", "trojan.exe");
    }

    public async Task StartAsync(Models.ServerConfig server, int localPort, string localAddr = "127.0.0.1", CancellationToken ct = default)
    {
        if (IsRunning) return;

        var trojanPath = GetTrojanPath();
        if (!File.Exists(trojanPath))
            throw new FileNotFoundException("trojan.exe not found. Place it in Tools/trojan/.", trojanPath);

        var configPath = WriteTrojanConfig(server, localPort, localAddr);

        _trojanProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = trojanPath,
                Arguments = $"-c \"{configPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(trojanPath)!
            },
            EnableRaisingEvents = true
        };

        _trojanProcess.OutputDataReceived += OnDataReceived;
        _trojanProcess.ErrorDataReceived += OnDataReceived;
        _trojanProcess.Exited += (_, _) => ProcessExited?.Invoke();

        try
        {
            _trojanProcess.Start();
            _trojanProcess.BeginOutputReadLine();
            _trojanProcess.BeginErrorReadLine();
            await Task.Delay(500, ct);
        }
        catch
        {
            // CR-01: prevent orphan process if cancellation or Start() throws
            Stop();
            throw;
        }
    }

    public void Stop()
    {
        try
        {
            if (_trojanProcess is { HasExited: false })
            {
                _trojanProcess.Kill(true);
                _trojanProcess.WaitForExit(3000);
            }
        }
        catch
        {
            // ignored
        }
        finally
        {
            _trojanProcess?.Dispose();
            _trojanProcess = null;
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

    internal string WriteTrojanConfig(Models.ServerConfig server, int localPort, string localAddr, string? configDirOverride = null)
    {
        // CR-05: validate critical fields before writing config
        if (string.IsNullOrWhiteSpace(server.RemoteAddr))
            throw new InvalidOperationException("Server remote address cannot be empty.");
        if (string.IsNullOrWhiteSpace(server.Password))
            throw new InvalidOperationException("Server password cannot be empty.");
        var safeLocalPort = Math.Clamp(localPort, 1, 65535);
        var safeRemotePort = Math.Clamp(server.RemotePort, 1, 65535);

        var configDir = configDirOverride ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "trojan4win");
        Directory.CreateDirectory(configDir);
        var configPath = Path.Combine(configDir, "trojan_config.json");

        var alpnList = new List<string>();
        foreach (var a in (server.Alpn ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
            alpnList.Add(a.Trim());

        var config = new Dictionary<string, object>
        {
            ["run_type"] = "client",
            ["local_addr"] = localAddr,
            ["local_port"] = safeLocalPort,
            ["remote_addr"] = server.RemoteAddr,
            ["remote_port"] = safeRemotePort,
            ["password"] = new[] { server.Password },
            ["log_level"] = server.TrojanLogLevel,
            ["ssl"] = new Dictionary<string, object>
            {
                ["verify"] = server.VerifyCert,
                ["verify_hostname"] = server.VerifyCert,
                ["cert"] = server.Cert,
                ["key"] = server.Key,
                ["cipher"] = server.Cipher,
                ["cipher_tls13"] = server.CipherTls13,
                ["sni"] = string.IsNullOrWhiteSpace(server.Sni) ? server.RemoteAddr : server.Sni,
                ["alpn"] = alpnList,
                ["reuse_session"] = server.ReuseSession,
                ["session_ticket"] = server.SessionTicket,
                ["curves"] = server.Curves
            },
            ["tcp"] = new Dictionary<string, object>
            {
                ["no_delay"] = server.NoDelay,
                ["keep_alive"] = server.KeepAlive,
                ["reuse_port"] = server.ReusePort,
                ["fast_open"] = server.FastOpen,
                ["fast_open_qlen"] = server.FastOpenQlen
            }
        };

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        // CR-16: write to a temp file then rename atomically to prevent a corrupt
        // trojan_config.json if the process is killed mid-write
        var tmpPath = configPath + ".tmp";
        File.WriteAllText(tmpPath, json);
        if (File.Exists(configPath))
            File.Replace(tmpPath, configPath, null);
        else
            File.Move(tmpPath, configPath);
        return configPath;
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
