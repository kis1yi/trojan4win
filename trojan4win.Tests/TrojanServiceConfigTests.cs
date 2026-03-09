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
}
