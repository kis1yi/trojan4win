using System;
using System.IO;
using System.Text.Json;
using trojan4win.Models;
using trojan4win.Services;
using Xunit;

namespace trojan4win.Tests;

public sealed class TrojanServiceConfigTests : IDisposable
{
    private readonly string _tempDir;
    private readonly TrojanService _svc;

    public TrojanServiceConfigTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "trojan4win_trojan_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _svc = new TrojanService();
    }

    public void Dispose()
    {
        _svc.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static ServerConfig ValidServer() => new()
    {
        RemoteAddr = "example.com",
        Password = "secret",
        RemotePort = 443
    };

    // ── structure ────────────────────────────────────────────────────────────

    [Fact]
    public void WriteTrojanConfig_ValidServer_ProducesRequiredTopLevelFields()
    {
        var configPath = _svc.WriteTrojanConfig(ValidServer(), 1080, "127.0.0.1", _tempDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        var root = doc.RootElement;

        Assert.Equal("client", root.GetProperty("run_type").GetString());
        Assert.Equal("127.0.0.1", root.GetProperty("local_addr").GetString());
        Assert.Equal(1080, root.GetProperty("local_port").GetInt32());
        Assert.Equal("example.com", root.GetProperty("remote_addr").GetString());
        Assert.Equal(443, root.GetProperty("remote_port").GetInt32());
        Assert.Equal(JsonValueKind.Object, root.GetProperty("ssl").ValueKind);
        Assert.Equal(JsonValueKind.Object, root.GetProperty("tcp").ValueKind);
    }

    [Fact]
    public void WriteTrojanConfig_ValidServer_PasswordIsArray()
    {
        var configPath = _svc.WriteTrojanConfig(ValidServer(), 1080, "127.0.0.1", _tempDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        var passwords = doc.RootElement.GetProperty("password");

        Assert.Equal(JsonValueKind.Array, passwords.ValueKind);
        Assert.Equal(1, passwords.GetArrayLength());
        Assert.Equal("secret", passwords[0].GetString());
    }

    // ── ALPN parsing ─────────────────────────────────────────────────────────

    [Fact]
    public void WriteTrojanConfig_EmptyAlpn_ProducesEmptyArray()
    {
        var server = ValidServer();
        server.Alpn = "";
        var configPath = _svc.WriteTrojanConfig(server, 1080, "127.0.0.1", _tempDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        var alpn = doc.RootElement.GetProperty("ssl").GetProperty("alpn");

        Assert.Equal(JsonValueKind.Array, alpn.ValueKind);
        Assert.Equal(0, alpn.GetArrayLength());
    }

    [Fact]
    public void WriteTrojanConfig_CommaSeparatedAlpn_ParsedIntoArray()
    {
        var server = ValidServer();
        server.Alpn = "h2,http/1.1";
        var configPath = _svc.WriteTrojanConfig(server, 1080, "127.0.0.1", _tempDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        var alpn = doc.RootElement.GetProperty("ssl").GetProperty("alpn");

        Assert.Equal(2, alpn.GetArrayLength());
        Assert.Equal("h2", alpn[0].GetString());
        Assert.Equal("http/1.1", alpn[1].GetString());
    }

    [Fact]
    public void WriteTrojanConfig_AlpnWithWhitespace_Stripped()
    {
        var server = ValidServer();
        server.Alpn = " h2 , http/1.1 ";
        var configPath = _svc.WriteTrojanConfig(server, 1080, "127.0.0.1", _tempDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        var alpn = doc.RootElement.GetProperty("ssl").GetProperty("alpn");

        Assert.Equal("h2", alpn[0].GetString());
        Assert.Equal("http/1.1", alpn[1].GetString());
    }

    // ── port clamping ─────────────────────────────────────────────────────────

    [Fact]
    public void WriteTrojanConfig_LocalPortZero_ClampsToOne()
    {
        var configPath = _svc.WriteTrojanConfig(ValidServer(), 0, "127.0.0.1", _tempDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        Assert.Equal(1, doc.RootElement.GetProperty("local_port").GetInt32());
    }

    [Fact]
    public void WriteTrojanConfig_LocalPortOverMax_ClampsTo65535()
    {
        var configPath = _svc.WriteTrojanConfig(ValidServer(), 99999, "127.0.0.1", _tempDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        Assert.Equal(65535, doc.RootElement.GetProperty("local_port").GetInt32());
    }

    [Fact]
    public void WriteTrojanConfig_RemotePortOverMax_ClampsTo65535()
    {
        var server = ValidServer();
        server.RemotePort = 99999;
        var configPath = _svc.WriteTrojanConfig(server, 1080, "127.0.0.1", _tempDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        Assert.Equal(65535, doc.RootElement.GetProperty("remote_port").GetInt32());
    }

    // ── validation (CR-05) ───────────────────────────────────────────────────

    [Fact]
    public void WriteTrojanConfig_EmptyRemoteAddr_ThrowsInvalidOperation()
    {
        var server = new ServerConfig { RemoteAddr = "", Password = "pw" };
        Assert.Throws<InvalidOperationException>(() =>
            _svc.WriteTrojanConfig(server, 1080, "127.0.0.1", _tempDir));
    }

    [Fact]
    public void WriteTrojanConfig_WhitespaceRemoteAddr_ThrowsInvalidOperation()
    {
        var server = new ServerConfig { RemoteAddr = "   ", Password = "pw" };
        Assert.Throws<InvalidOperationException>(() =>
            _svc.WriteTrojanConfig(server, 1080, "127.0.0.1", _tempDir));
    }

    [Fact]
    public void WriteTrojanConfig_EmptyPassword_ThrowsInvalidOperation()
    {
        var server = new ServerConfig { RemoteAddr = "example.com", Password = "" };
        Assert.Throws<InvalidOperationException>(() =>
            _svc.WriteTrojanConfig(server, 1080, "127.0.0.1", _tempDir));
    }

    // ── atomic write (CR-16) ─────────────────────────────────────────────────

    [Fact]
    public void WriteTrojanConfig_AtomicWrite_TmpFileDoesNotPersist()
    {
        _svc.WriteTrojanConfig(ValidServer(), 1080, "127.0.0.1", _tempDir);
        Assert.False(File.Exists(Path.Combine(_tempDir, "trojan_config.json.tmp")),
            "CR-16: .tmp file must not remain after write");
    }

    // ── SNI fallback ─────────────────────────────────────────────────────────

    [Fact]
    public void WriteTrojanConfig_SniEmpty_FallsBackToRemoteAddr()
    {
        var server = ValidServer();
        server.Sni = "";
        var configPath = _svc.WriteTrojanConfig(server, 1080, "127.0.0.1", _tempDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        Assert.Equal(server.RemoteAddr,
            doc.RootElement.GetProperty("ssl").GetProperty("sni").GetString());
    }

    [Fact]
    public void WriteTrojanConfig_SniProvided_UsesSni()
    {
        var server = ValidServer();
        server.Sni = "custom.sni.example.com";
        var configPath = _svc.WriteTrojanConfig(server, 1080, "127.0.0.1", _tempDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        Assert.Equal("custom.sni.example.com",
            doc.RootElement.GetProperty("ssl").GetProperty("sni").GetString());
    }

    // ── log_level ────────────────────────────────────────────────────────────

    [Fact]
    public void WriteTrojanConfig_LogLevel_WrittenFromServer()
    {
        var server = ValidServer();
        server.TrojanLogLevel = 3;
        var configPath = _svc.WriteTrojanConfig(server, 1080, "127.0.0.1", _tempDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        Assert.Equal(3, doc.RootElement.GetProperty("log_level").GetInt32());
    }

    // ── TCP block ────────────────────────────────────────────────────────────

    [Fact]
    public void WriteTrojanConfig_TcpBlock_ContainsAllFields()
    {
        var server = ValidServer();
        server.NoDelay = false;
        server.KeepAlive = true;
        server.PreferIpv4 = true;
        var configPath = _svc.WriteTrojanConfig(server, 1080, "127.0.0.1", _tempDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        var tcp = doc.RootElement.GetProperty("tcp");

        Assert.False(tcp.GetProperty("no_delay").GetBoolean());
        Assert.True(tcp.GetProperty("keep_alive").GetBoolean());
        Assert.True(tcp.GetProperty("prefer_ipv4").GetBoolean());
    }

    // ── SSL fingerprint (conditional) ────────────────────────────────────────

    [Fact]
    public void WriteTrojanConfig_FingerprintEmpty_KeyOmitted()
    {
        var server = ValidServer();
        server.Fingerprint = "";
        var configPath = _svc.WriteTrojanConfig(server, 1080, "127.0.0.1", _tempDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        var ssl = doc.RootElement.GetProperty("ssl");
        Assert.False(ssl.TryGetProperty("fingerprint", out _));
    }

    [Fact]
    public void WriteTrojanConfig_FingerprintSet_Written()
    {
        var server = ValidServer();
        server.Fingerprint = "firefox";
        var configPath = _svc.WriteTrojanConfig(server, 1080, "127.0.0.1", _tempDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        Assert.Equal("firefox",
            doc.RootElement.GetProperty("ssl").GetProperty("fingerprint").GetString());
    }

    // ── SSL ech/ech_config (conditional pair) ────────────────────────────────

    [Fact]
    public void WriteTrojanConfig_EchDisabled_BothKeysOmitted()
    {
        var server = ValidServer();
        server.Ech = false;
        server.EchConfig = "should-be-ignored";
        var configPath = _svc.WriteTrojanConfig(server, 1080, "127.0.0.1", _tempDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        var ssl = doc.RootElement.GetProperty("ssl");
        Assert.False(ssl.TryGetProperty("ech", out _));
        Assert.False(ssl.TryGetProperty("ech_config", out _));
    }

    [Fact]
    public void WriteTrojanConfig_EchEnabled_BothKeysWritten()
    {
        var server = ValidServer();
        server.Ech = true;
        server.EchConfig = "AEj+DQBEb...";
        var configPath = _svc.WriteTrojanConfig(server, 1080, "127.0.0.1", _tempDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        var ssl = doc.RootElement.GetProperty("ssl");
        Assert.True(ssl.GetProperty("ech").GetBoolean());
        Assert.Equal("AEj+DQBEb...", ssl.GetProperty("ech_config").GetString());
    }

    // ── SSL cert / curves passthrough ────────────────────────────────────────

    [Fact]
    public void WriteTrojanConfig_CertAndCurves_WrittenToSsl()
    {
        var server = ValidServer();
        server.Cert = "/path/to/cert.pem";
        server.Curves = "X25519:P-256";
        var configPath = _svc.WriteTrojanConfig(server, 1080, "127.0.0.1", _tempDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        var ssl = doc.RootElement.GetProperty("ssl");
        Assert.Equal("/path/to/cert.pem", ssl.GetProperty("cert").GetString());
        Assert.Equal("X25519:P-256", ssl.GetProperty("curves").GetString());
    }

    // ── mux (conditional block) ──────────────────────────────────────────────

    [Fact]
    public void WriteTrojanConfig_MuxDisabled_BlockAbsent()
    {
        var server = ValidServer();
        server.MuxEnabled = false;
        var configPath = _svc.WriteTrojanConfig(server, 1080, "127.0.0.1", _tempDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        Assert.False(doc.RootElement.TryGetProperty("mux", out _));
    }

    [Fact]
    public void WriteTrojanConfig_MuxEnabled_BlockContainsAllFields()
    {
        var server = ValidServer();
        server.MuxEnabled = true;
        server.MuxConcurrency = 16;
        server.MuxIdleTimeout = 60;
        server.MuxStreamBuffer = 2_000_000;
        server.MuxReceiveBuffer = 2_000_000;
        server.MuxProtocol = 1;
        var configPath = _svc.WriteTrojanConfig(server, 1080, "127.0.0.1", _tempDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        var mux = doc.RootElement.GetProperty("mux");

        Assert.True(mux.GetProperty("enabled").GetBoolean());
        Assert.Equal(16, mux.GetProperty("concurrency").GetInt32());
        Assert.Equal(60, mux.GetProperty("idle_timeout").GetInt32());
        Assert.Equal(2_000_000, mux.GetProperty("stream_buffer").GetInt32());
        Assert.Equal(2_000_000, mux.GetProperty("receive_buffer").GetInt32());
        Assert.Equal(1, mux.GetProperty("protocol").GetInt32());
    }

    [Fact]
    public void WriteTrojanConfig_MuxReceiveBufferBelowStream_ClampedUpward()
    {
        var server = ValidServer();
        server.MuxEnabled = true;
        server.MuxStreamBuffer = 8_000_000;
        server.MuxReceiveBuffer = 4_000_000;
        var configPath = _svc.WriteTrojanConfig(server, 1080, "127.0.0.1", _tempDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        var mux = doc.RootElement.GetProperty("mux");

        Assert.Equal(8_000_000, mux.GetProperty("stream_buffer").GetInt32());
        Assert.Equal(8_000_000, mux.GetProperty("receive_buffer").GetInt32());
    }

    // ── websocket (conditional block) ────────────────────────────────────────

    [Fact]
    public void WriteTrojanConfig_WebsocketDisabled_BlockAbsent()
    {
        var server = ValidServer();
        server.WebsocketEnabled = false;
        server.WebsocketPath = "/ws";
        var configPath = _svc.WriteTrojanConfig(server, 1080, "127.0.0.1", _tempDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        Assert.False(doc.RootElement.TryGetProperty("websocket", out _));
    }

    [Fact]
    public void WriteTrojanConfig_WebsocketEnabled_FieldsWritten()
    {
        var server = ValidServer();
        server.WebsocketEnabled = true;
        server.WebsocketPath = "/ws";
        server.WebsocketHost = "ws.example.com";
        var configPath = _svc.WriteTrojanConfig(server, 1080, "127.0.0.1", _tempDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        var ws = doc.RootElement.GetProperty("websocket");
        Assert.True(ws.GetProperty("enabled").GetBoolean());
        Assert.Equal("/ws", ws.GetProperty("path").GetString());
        Assert.Equal("ws.example.com", ws.GetProperty("host").GetString());
    }

    // ── shadowsocks (conditional block) ──────────────────────────────────────

    [Fact]
    public void WriteTrojanConfig_ShadowsocksDisabled_BlockAbsent()
    {
        var server = ValidServer();
        server.ShadowsocksEnabled = false;
        var configPath = _svc.WriteTrojanConfig(server, 1080, "127.0.0.1", _tempDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        Assert.False(doc.RootElement.TryGetProperty("shadowsocks", out _));
    }

    [Fact]
    public void WriteTrojanConfig_ShadowsocksEnabled_FieldsWritten()
    {
        var server = ValidServer();
        server.ShadowsocksEnabled = true;
        server.ShadowsocksMethod = "AES-256-GCM";
        server.ShadowsocksPassword = "sspass";
        var configPath = _svc.WriteTrojanConfig(server, 1080, "127.0.0.1", _tempDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        var ss = doc.RootElement.GetProperty("shadowsocks");
        Assert.True(ss.GetProperty("enabled").GetBoolean());
        Assert.Equal("AES-256-GCM", ss.GetProperty("method").GetString());
        Assert.Equal("sspass", ss.GetProperty("password").GetString());
    }

    // ── router (conditional block + rule grouping) ───────────────────────────

    [Fact]
    public void WriteTrojanConfig_RouterDisabled_BlockAbsent()
    {
        var server = ValidServer();
        server.RouterEnabled = false;
        server.RouterRules.Add(new RouterRule { Policy = "proxy", Type = "domain", Value = "a.com" });
        var configPath = _svc.WriteTrojanConfig(server, 1080, "127.0.0.1", _tempDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        Assert.False(doc.RootElement.TryGetProperty("router", out _));
    }

    [Fact]
    public void WriteTrojanConfig_RouterEnabled_FieldsWritten()
    {
        var server = ValidServer();
        server.RouterEnabled = true;
        server.RouterDefaultPolicy = "bypass";
        server.RouterDomainStrategy = "ip_if_non_match";
        server.RouterGeoip = "my-geoip.dat";
        server.RouterGeosite = "my-geosite.dat";
        var configPath = _svc.WriteTrojanConfig(server, 1080, "127.0.0.1", _tempDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        var router = doc.RootElement.GetProperty("router");

        Assert.True(router.GetProperty("enabled").GetBoolean());
        Assert.Equal("bypass", router.GetProperty("default_policy").GetString());
        Assert.Equal("ip_if_non_match", router.GetProperty("domain_strategy").GetString());
        Assert.Equal("my-geoip.dat", router.GetProperty("geoip").GetString());
        Assert.Equal("my-geosite.dat", router.GetProperty("geosite").GetString());
    }

    [Fact]
    public void WriteTrojanConfig_RouterRules_GroupedByPolicy_AsTypeColonValue()
    {
        var server = ValidServer();
        server.RouterEnabled = true;
        server.RouterRules.Add(new RouterRule { Policy = "bypass", Type = "cidr", Value = "10.0.0.0/8" });
        server.RouterRules.Add(new RouterRule { Policy = "bypass", Type = "geoip", Value = "cn" });
        server.RouterRules.Add(new RouterRule { Policy = "proxy", Type = "domain", Value = "example.com" });
        server.RouterRules.Add(new RouterRule { Policy = "block", Type = "full", Value = "ads.example.com" });
        // skipped: empty policy
        server.RouterRules.Add(new RouterRule { Policy = "", Type = "domain", Value = "skip1" });
        // skipped: empty type
        server.RouterRules.Add(new RouterRule { Policy = "proxy", Type = "", Value = "skip2" });

        var configPath = _svc.WriteTrojanConfig(server, 1080, "127.0.0.1", _tempDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        var router = doc.RootElement.GetProperty("router");

        var bypass = router.GetProperty("bypass");
        Assert.Equal(2, bypass.GetArrayLength());
        Assert.Equal("cidr:10.0.0.0/8", bypass[0].GetString());
        Assert.Equal("geoip:cn", bypass[1].GetString());

        var proxy = router.GetProperty("proxy");
        Assert.Equal(1, proxy.GetArrayLength());
        Assert.Equal("domain:example.com", proxy[0].GetString());

        var block = router.GetProperty("block");
        Assert.Equal(1, block.GetArrayLength());
        Assert.Equal("full:ads.example.com", block[0].GetString());
    }

    [Fact]
    public void WriteTrojanConfig_RouterEnabled_NoRules_AllArraysEmpty()
    {
        var server = ValidServer();
        server.RouterEnabled = true;
        var configPath = _svc.WriteTrojanConfig(server, 1080, "127.0.0.1", _tempDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        var router = doc.RootElement.GetProperty("router");
        Assert.Equal(0, router.GetProperty("bypass").GetArrayLength());
        Assert.Equal(0, router.GetProperty("proxy").GetArrayLength());
        Assert.Equal(0, router.GetProperty("block").GetArrayLength());
    }

    // ── forward_proxy (conditional block) ────────────────────────────────────

    [Fact]
    public void WriteTrojanConfig_ForwardProxyDisabled_BlockAbsent()
    {
        var server = ValidServer();
        server.ForwardProxyEnabled = false;
        server.ForwardProxyAddr = "10.0.0.1";
        var configPath = _svc.WriteTrojanConfig(server, 1080, "127.0.0.1", _tempDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        Assert.False(doc.RootElement.TryGetProperty("forward_proxy", out _));
    }

    [Fact]
    public void WriteTrojanConfig_ForwardProxyEnabled_FieldsWritten()
    {
        var server = ValidServer();
        server.ForwardProxyEnabled = true;
        server.ForwardProxyAddr = "10.0.0.1";
        server.ForwardProxyPort = 3128;
        server.ForwardProxyUsername = "user";
        server.ForwardProxyPassword = "pw";
        var configPath = _svc.WriteTrojanConfig(server, 1080, "127.0.0.1", _tempDir);
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        var fp = doc.RootElement.GetProperty("forward_proxy");
        Assert.True(fp.GetProperty("enabled").GetBoolean());
        Assert.Equal("10.0.0.1", fp.GetProperty("proxy_addr").GetString());
        Assert.Equal(3128, fp.GetProperty("proxy_port").GetInt32());
        Assert.Equal("user", fp.GetProperty("username").GetString());
        Assert.Equal("pw", fp.GetProperty("password").GetString());
    }

    // ── removed keys (forward compatibility with trojan-go schema) ───────────

    [Fact]
    public void WriteTrojanConfig_DefaultServer_LegacyKeysAbsent()
    {
        var configPath = _svc.WriteTrojanConfig(ValidServer(), 1080, "127.0.0.1", _tempDir);
        var raw = File.ReadAllText(configPath);
        Assert.DoesNotContain("\"cipher\"", raw);
        Assert.DoesNotContain("\"cipher_tls13\"", raw);
        Assert.DoesNotContain("\"reuse_session\"", raw);
        Assert.DoesNotContain("\"session_ticket\"", raw);
        Assert.DoesNotContain("\"fast_open\"", raw);
        Assert.DoesNotContain("\"fast_open_qlen\"", raw);
        Assert.DoesNotContain("\"reuse_port\"", raw);
    }

    // ── ForwardProxy field names in my service ──────────────────────────────
    // (no Key field emitted to the top level; private key isn't used client-side)
    [Fact]
    public void WriteTrojanConfig_DoesNotEmitKey_AtTopLevel()
    {
        var server = ValidServer();
        server.Key = "/path/to/private.key";
        var configPath = _svc.WriteTrojanConfig(server, 1080, "127.0.0.1", _tempDir);
        var raw = File.ReadAllText(configPath);
        Assert.DoesNotContain("/path/to/private.key", raw);
    }
}
