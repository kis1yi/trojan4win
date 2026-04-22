using System;
using System.Collections.Generic;
using System.IO;
using trojan4win.Models;
using trojan4win.Services;
using Xunit;

namespace trojan4win.Tests;

// Each test method gets a fresh temp directory; _testSettingsDir is reset in Dispose.
// Tests within a class run sequentially in xunit so the shared static is safe.
public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _tempDir;

    public SettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "trojan4win_tests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        SettingsService._testSettingsDir = _tempDir;
    }

    public void Dispose()
    {
        SettingsService._testSettingsDir = null;
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Save_ThenLoad_RoundTrip_PreservesAllFields()
    {
        var server = new ServerConfig
        {
            Name = "Test Server",
            RemoteAddr = "example.com",
            RemotePort = 8443,
            Password = "secret",
            VerifyCert = false,
            TrojanLogLevel = 3
        };
        var settings = new AppSettings
        {
            LastSelectedServerId = "srv-1",
            AutoStart = true,
            AutoConnect = true,
            MinimizeToTray = false,
            LocalSocksPort = 9090,
            LocalAddr = "192.168.1.1",
            ProxyLogLevel = "Debug",
            SupportedProtocols = new List<string> { "TCP" },
            ExcludedProcesses = new List<string> { "chrome.exe" },
            Servers = new List<ServerConfig> { server }
        };

        SettingsService.Save(settings);
        var loaded = SettingsService.Load();

        Assert.Equal("srv-1", loaded.LastSelectedServerId);
        Assert.True(loaded.AutoStart);
        Assert.True(loaded.AutoConnect);
        Assert.False(loaded.MinimizeToTray);
        Assert.Equal(9090, loaded.LocalSocksPort);
        Assert.Equal("192.168.1.1", loaded.LocalAddr);
        Assert.Equal("Debug", loaded.ProxyLogLevel);
        Assert.Equal(new List<string> { "TCP" }, loaded.SupportedProtocols);
        Assert.Equal(new List<string> { "chrome.exe" }, loaded.ExcludedProcesses);
        Assert.Single(loaded.Servers);
        Assert.Equal("Test Server", loaded.Servers[0].Name);
        Assert.Equal("example.com", loaded.Servers[0].RemoteAddr);
        Assert.Equal(8443, loaded.Servers[0].RemotePort);
        Assert.False(loaded.Servers[0].VerifyCert);
        Assert.Equal(3, loaded.Servers[0].TrojanLogLevel);
    }

    [Fact]
    public void Load_WhenNoFileExists_ReturnsDefaults()
    {
        var loaded = SettingsService.Load();

        Assert.NotNull(loaded);
        Assert.Empty(loaded.Servers);
        Assert.Null(loaded.LastSelectedServerId);
        Assert.False(loaded.AutoStart);
        Assert.False(loaded.AutoConnect);
        Assert.True(loaded.MinimizeToTray);
        Assert.Equal(1080, loaded.LocalSocksPort);
        Assert.Equal("127.0.0.1", loaded.LocalAddr);
        Assert.Equal(new List<string> { "TCP", "UDP" }, loaded.SupportedProtocols);
        Assert.Equal("Info", loaded.ProxyLogLevel);
        Assert.Empty(loaded.ExcludedProcesses);
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaultsAndCreatesBakFile()
    {
        var settingsFile = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(settingsFile, "{ this is not valid json {{{");

        var loaded = SettingsService.Load();

        Assert.NotNull(loaded);
        Assert.Empty(loaded.Servers);
        Assert.True(File.Exists(settingsFile + ".bak"), "CR-15: corrupt file must be renamed to .bak");
        Assert.False(File.Exists(settingsFile), "corrupt file must be moved away");
    }

    [Fact]
    public void Save_AtomicWrite_TmpFileDoesNotPersist()
    {
        SettingsService.Save(new AppSettings { LocalSocksPort = 7777 });

        Assert.False(File.Exists(Path.Combine(_tempDir, "settings.json.tmp")),
            "CR-14: .tmp file must not remain after Save()");
        Assert.True(File.Exists(Path.Combine(_tempDir, "settings.json")));
    }

    [Fact]
    public void Save_ThenLoad_RoundTrip_PreservesAllNewServerConfigFields()
    {
        var server = new ServerConfig
        {
            Name = "Full Server",
            Region = "EU",
            RemoteAddr = "example.com",
            RemotePort = 8443,
            Password = "pw",
            VerifyCert = false,
            Sni = "custom.sni",
            Alpn = "h2,http/1.1",
            Cert = "/path/cert.pem",
            Key = "/path/key.pem",
            Curves = "P-256",
            Fingerprint = "firefox",
            Ech = true,
            EchConfig = "AEj+DQBEb...",
            NoDelay = false,
            KeepAlive = false,
            PreferIpv4 = true,
            MuxEnabled = true,
            MuxConcurrency = 16,
            MuxIdleTimeout = 60,
            MuxStreamBuffer = 8388608,
            MuxReceiveBuffer = 8388608,
            MuxProtocol = 1,
            WebsocketEnabled = true,
            WebsocketPath = "/ws",
            WebsocketHost = "ws.example.com",
            ShadowsocksEnabled = true,
            ShadowsocksMethod = "AES-256-GCM",
            ShadowsocksPassword = "sspass",
            RouterEnabled = true,
            RouterDefaultPolicy = "bypass",
            RouterDomainStrategy = "ip_if_non_match",
            RouterGeoip = "custom-geoip.dat",
            RouterGeosite = "custom-geosite.dat",
            ForwardProxyEnabled = true,
            ForwardProxyAddr = "10.0.0.1",
            ForwardProxyPort = 3128,
            ForwardProxyUsername = "fpuser",
            ForwardProxyPassword = "fppw",
            TrojanLogLevel = 4
        };
        server.RouterRules.Add(new RouterRule { Policy = "bypass", Type = "cidr", Value = "10.0.0.0/8" });
        server.RouterRules.Add(new RouterRule { Policy = "proxy", Type = "domain", Value = "example.com" });
        server.RouterRules.Add(new RouterRule { Policy = "block", Type = "geoip", Value = "cn" });

        var settings = new AppSettings { Servers = new List<ServerConfig> { server } };

        SettingsService.Save(settings);
        var loaded = SettingsService.Load();
        var s = Assert.Single(loaded.Servers);

        Assert.Equal("Full Server", s.Name);
        Assert.Equal("EU", s.Region);
        Assert.Equal("example.com", s.RemoteAddr);
        Assert.Equal(8443, s.RemotePort);
        Assert.Equal("pw", s.Password);
        Assert.False(s.VerifyCert);
        Assert.Equal("custom.sni", s.Sni);
        Assert.Equal("h2,http/1.1", s.Alpn);
        Assert.Equal("/path/cert.pem", s.Cert);
        Assert.Equal("/path/key.pem", s.Key);
        Assert.Equal("P-256", s.Curves);
        Assert.Equal("firefox", s.Fingerprint);
        Assert.True(s.Ech);
        Assert.Equal("AEj+DQBEb...", s.EchConfig);
        Assert.False(s.NoDelay);
        Assert.False(s.KeepAlive);
        Assert.True(s.PreferIpv4);
        Assert.True(s.MuxEnabled);
        Assert.Equal(16, s.MuxConcurrency);
        Assert.Equal(60, s.MuxIdleTimeout);
        Assert.Equal(8388608, s.MuxStreamBuffer);
        Assert.Equal(8388608, s.MuxReceiveBuffer);
        Assert.Equal(1, s.MuxProtocol);
        Assert.True(s.WebsocketEnabled);
        Assert.Equal("/ws", s.WebsocketPath);
        Assert.Equal("ws.example.com", s.WebsocketHost);
        Assert.True(s.ShadowsocksEnabled);
        Assert.Equal("AES-256-GCM", s.ShadowsocksMethod);
        Assert.Equal("sspass", s.ShadowsocksPassword);
        Assert.True(s.RouterEnabled);
        Assert.Equal("bypass", s.RouterDefaultPolicy);
        Assert.Equal("ip_if_non_match", s.RouterDomainStrategy);
        Assert.Equal("custom-geoip.dat", s.RouterGeoip);
        Assert.Equal("custom-geosite.dat", s.RouterGeosite);
        Assert.True(s.ForwardProxyEnabled);
        Assert.Equal("10.0.0.1", s.ForwardProxyAddr);
        Assert.Equal(3128, s.ForwardProxyPort);
        Assert.Equal("fpuser", s.ForwardProxyUsername);
        Assert.Equal("fppw", s.ForwardProxyPassword);
        Assert.Equal(4, s.TrojanLogLevel);

        Assert.Equal(3, s.RouterRules.Count);
        Assert.Equal("bypass", s.RouterRules[0].Policy);
        Assert.Equal("cidr", s.RouterRules[0].Type);
        Assert.Equal("10.0.0.0/8", s.RouterRules[0].Value);
        Assert.Equal("proxy", s.RouterRules[1].Policy);
        Assert.Equal("domain", s.RouterRules[1].Type);
        Assert.Equal("example.com", s.RouterRules[1].Value);
        Assert.Equal("block", s.RouterRules[2].Policy);
        Assert.Equal("geoip", s.RouterRules[2].Type);
        Assert.Equal("cn", s.RouterRules[2].Value);
    }
}
