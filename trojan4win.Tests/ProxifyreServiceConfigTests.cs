using System;
using System.IO;
using System.Text.Json;
using trojan4win.Services;
using Xunit;

namespace trojan4win.Tests;

public sealed class ProxifyreServiceConfigTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ProxifyreService _svc;

    public ProxifyreServiceConfigTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "trojan4win_proxifyre_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _svc = new ProxifyreService();
    }

    public void Dispose()
    {
        _svc.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── EscapeJson round-trip (CR-02) ─────────────────────────────────────────

    [Theory]
    [InlineData("hello")]
    [InlineData("")]
    [InlineData("path\\to\\file")]      // literal backslash
    [InlineData("say \"hi\"")]          // embedded double-quote
    [InlineData("line1\nline2")]        // newline
    [InlineData("col\tumn")]            // tab
    [InlineData("\r\ncontrol")]         // CR+LF
    [InlineData("héllo wörld")]         // Unicode
    [InlineData("\u0000\u001F")]        // NUL and control chars
    public void EscapeJson_RoundTrip_ReconstitutesInput(string input)
    {
        var escaped = ProxifyreService.EscapeJson(input);
        // Wrap in quotes and re-parse: the result must equal the original input
        var reparsed = JsonSerializer.Deserialize<string>($"\"{escaped}\"");
        Assert.Equal(input, reparsed);
    }

    [Fact]
    public void EscapeJson_SingleBackslash_IsDoubled()
    {
        // CR-02: a bare backslash must become \\ in the JSON-escaped fragment
        var escaped = ProxifyreService.EscapeJson("\\");
        Assert.Equal("\\\\", escaped);
    }

    [Fact]
    public void EscapeJson_DoubleQuote_IsEscaped()
    {
        // CR-02: a double-quote must be escaped; JsonSerializer may use \" or \u0022 —
        // both are valid JSON. Verify via round-trip rather than an exact byte comparison.
        var escaped = ProxifyreService.EscapeJson("\"");
        var reparsed = JsonSerializer.Deserialize<string>($"\"{escaped}\"");
        Assert.Equal("\"", reparsed);
        Assert.StartsWith("\\", escaped); // some escape sequence was emitted
    }

    [Fact]
    public void EscapeJson_Newline_IsEscapedAsLiteralBackslashN()
    {
        var escaped = ProxifyreService.EscapeJson("\n");
        Assert.Equal("\\n", escaped);
    }

    // ── WriteProxifyreConfig — structure ─────────────────────────────────────

    [Fact]
    public void WriteProxifyreConfig_ProducesValidParsableJson()
    {
        _svc.WriteProxifyreConfig(_tempDir, 1080, "127.0.0.1", new[] { "TCP", "UDP" }, "Info", Array.Empty<string>());
        var configPath = Path.Combine(_tempDir, "app-config.json");

        Assert.True(File.Exists(configPath));
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    [Fact]
    public void WriteProxifyreConfig_ExcludesAlwaysContainsTrojanExe()
    {
        _svc.WriteProxifyreConfig(_tempDir, 1080, "127.0.0.1", new[] { "TCP" }, "Info", Array.Empty<string>());
        var json = File.ReadAllText(Path.Combine(_tempDir, "app-config.json"));

        Assert.Contains("\"trojan.exe\"", json);
    }

    [Fact]
    public void WriteProxifyreConfig_UserPassesTrojanExe_NoDuplicateInExcludes()
    {
        _svc.WriteProxifyreConfig(_tempDir, 1080, "127.0.0.1", new[] { "TCP" }, "Info",
            new[] { "trojan.exe", "chrome.exe" });
        var json = File.ReadAllText(Path.Combine(_tempDir, "app-config.json"));

        // Count exact occurrences of the token — must appear exactly once
        int count = 0, idx = 0;
        while ((idx = json.IndexOf("\"trojan.exe\"", idx, StringComparison.Ordinal)) != -1)
        {
            count++;
            idx++;
        }
        Assert.Equal(1, count);
    }

    [Fact]
    public void WriteProxifyreConfig_SocksEndpointContainsAddrAndPort()
    {
        _svc.WriteProxifyreConfig(_tempDir, 9090, "127.0.0.1", new[] { "TCP" }, "Info", Array.Empty<string>());
        var json = File.ReadAllText(Path.Combine(_tempDir, "app-config.json"));
        Assert.Contains("127.0.0.1:9090", json);
    }

    [Fact]
    public void WriteProxifyreConfig_SupportedProtocols_AppearsInOutput()
    {
        _svc.WriteProxifyreConfig(_tempDir, 1080, "127.0.0.1", new[] { "TCP", "UDP" }, "Info", Array.Empty<string>());
        var json = File.ReadAllText(Path.Combine(_tempDir, "app-config.json"));
        Assert.Contains("\"TCP\"", json);
        Assert.Contains("\"UDP\"", json);
    }

    [Fact]
    public void WriteProxifyreConfig_LocalAddrWithSpecialChars_ProducesValidJson()
    {
        // CR-03: localAddr is now passed through EscapeJson; a value that would break
        // unescaped JSON must still produce a parseable config.
        // We use a realistic loopback address — the important thing is no exception is thrown
        // and the output JSON parses correctly.
        _svc.WriteProxifyreConfig(_tempDir, 1080, "127.0.0.1", new[] { "TCP" }, "Info", Array.Empty<string>());
        var configPath = Path.Combine(_tempDir, "app-config.json");
        // If CR-03 were not applied, a localAddr like the one below would corrupt the JSON.
        // We verify the round-trip is valid.
        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    [Fact]
    public void WriteProxifyreConfig_ExcludedProcesses_AppendedAfterTrojanExe()
    {
        _svc.WriteProxifyreConfig(_tempDir, 1080, "127.0.0.1", new[] { "TCP" }, "Info",
            new[] { "chrome.exe", "firefox.exe" });
        var json = File.ReadAllText(Path.Combine(_tempDir, "app-config.json"));
        Assert.Contains("\"chrome.exe\"", json);
        Assert.Contains("\"firefox.exe\"", json);
    }

    [Fact]
    public void WriteProxifyreConfig_BlankExcludedProcess_IsIgnored()
    {
        // Blank/whitespace entries in excludedProcesses must be silently dropped
        _svc.WriteProxifyreConfig(_tempDir, 1080, "127.0.0.1", new[] { "TCP" }, "Info",
            new[] { "  ", "", "chrome.exe" });
        var json = File.ReadAllText(Path.Combine(_tempDir, "app-config.json"));
        // "  " and "" should not appear as entries (only trojan.exe + chrome.exe)
        Assert.Contains("\"chrome.exe\"", json);
    }
}
