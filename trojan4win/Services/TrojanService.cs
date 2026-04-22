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
        return Path.Combine(baseDir, "Tools", "trojan", "trojan-go.exe");
    }

    public async Task StartAsync(Models.ServerConfig server, int localPort, string localAddr = "127.0.0.1", CancellationToken ct = default)
    {
        if (IsRunning) return;

        var trojanPath = GetTrojanPath();
        if (!File.Exists(trojanPath))
            throw new FileNotFoundException("trojan-go.exe not found. Place it in Tools/trojan/.", trojanPath);

        var configPath = WriteTrojanConfig(server, localPort, localAddr);

        _trojanProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = trojanPath,
                Arguments = $"-config \"{configPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                // trojan-go resolves default geoip.dat / geosite.dat relative to its working
                // directory, so launch it from the binary's folder
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
                // trojan-go has no Windows clean-shutdown signal handler — kill the whole tree
                _trojanProcess.Kill(entireProcessTree: true);
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

        // ssl block
        var sslSni = string.IsNullOrWhiteSpace(server.Sni) ? server.RemoteAddr : server.Sni;
        var ssl = new Dictionary<string, object>
        {
            ["verify"] = server.VerifyCert,
            ["verify_hostname"] = server.VerifyCert,
            ["cert"] = server.Cert,
            ["sni"] = sslSni,
            ["alpn"] = alpnList,
            ["curves"] = server.Curves
        };
        if (!string.IsNullOrEmpty(server.Fingerprint))
            ssl["fingerprint"] = server.Fingerprint;
        if (server.Ech)
        {
            ssl["ech"] = true;
            ssl["ech_config"] = server.EchConfig;
        }

        var tcp = new Dictionary<string, object>
        {
            ["no_delay"] = server.NoDelay,
            ["keep_alive"] = server.KeepAlive,
            ["prefer_ipv4"] = server.PreferIpv4
        };

        var config = new Dictionary<string, object>
        {
            ["run_type"] = "client",
            ["local_addr"] = localAddr,
            ["local_port"] = safeLocalPort,
            ["remote_addr"] = server.RemoteAddr,
            ["remote_port"] = safeRemotePort,
            ["password"] = new[] { server.Password },
            ["log_level"] = server.TrojanLogLevel,
            ["ssl"] = ssl,
            ["tcp"] = tcp
        };

        if (server.MuxEnabled)
        {
            // trojan-go requires receive_buffer ≥ stream_buffer; clamp upward silently
            var receiveBuffer = server.MuxReceiveBuffer < server.MuxStreamBuffer
                ? server.MuxStreamBuffer
                : server.MuxReceiveBuffer;
            config["mux"] = new Dictionary<string, object>
            {
                ["enabled"] = true,
                ["concurrency"] = server.MuxConcurrency,
                ["idle_timeout"] = server.MuxIdleTimeout,
                ["stream_buffer"] = server.MuxStreamBuffer,
                ["receive_buffer"] = receiveBuffer,
                ["protocol"] = server.MuxProtocol
            };
        }

        if (server.WebsocketEnabled)
        {
            config["websocket"] = new Dictionary<string, object>
            {
                ["enabled"] = true,
                ["path"] = server.WebsocketPath,
                ["host"] = server.WebsocketHost
            };
        }

        if (server.ShadowsocksEnabled)
        {
            config["shadowsocks"] = new Dictionary<string, object>
            {
                ["enabled"] = true,
                ["method"] = server.ShadowsocksMethod,
                ["password"] = server.ShadowsocksPassword
            };
        }

        if (server.RouterEnabled)
        {
            var bypass = new List<string>();
            var proxy = new List<string>();
            var block = new List<string>();
            foreach (var rule in server.RouterRules)
            {
                if (string.IsNullOrWhiteSpace(rule.Policy) || string.IsNullOrWhiteSpace(rule.Type))
                    continue;
                var encoded = rule.Type + ":" + rule.Value;
                switch (rule.Policy)
                {
                    case "bypass": bypass.Add(encoded); break;
                    case "proxy": proxy.Add(encoded); break;
                    case "block": block.Add(encoded); break;
                }
            }
            config["router"] = new Dictionary<string, object>
            {
                ["enabled"] = true,
                ["default_policy"] = server.RouterDefaultPolicy,
                ["domain_strategy"] = server.RouterDomainStrategy,
                ["geoip"] = server.RouterGeoip,
                ["geosite"] = server.RouterGeosite,
                ["bypass"] = bypass,
                ["proxy"] = proxy,
                ["block"] = block
            };
        }

        if (server.ForwardProxyEnabled)
        {
            config["forward_proxy"] = new Dictionary<string, object>
            {
                ["enabled"] = true,
                ["proxy_addr"] = server.ForwardProxyAddr,
                ["proxy_port"] = server.ForwardProxyPort,
                ["username"] = server.ForwardProxyUsername,
                ["password"] = server.ForwardProxyPassword
            };
        }

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
