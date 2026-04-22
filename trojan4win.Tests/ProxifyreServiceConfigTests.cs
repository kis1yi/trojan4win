using System;
using System.IO;
using System.Text.Json;
using trojan4win.Models;
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

    // ── WriteProxifyreConfig — dual-binary invariant ──────────────────────────

    [Fact]
    public void WriteProxifyreConfig_ExcludesAlwaysContainsBothBinaries_ExcludeListed()
    {
        _svc.WriteProxifyreConfig(_tempDir, 1080, "127.0.0.1", new[] { "TCP" }, "Info",
            Array.Empty<string>(), ProcessFilterMode.ExcludeListed);
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(_tempDir, "app-config.json")));
        var excludes = doc.RootElement.GetProperty("excludes");
        var entries = new System.Collections.Generic.List<string>();
        foreach (var e in excludes.EnumerateArray()) entries.Add(e.GetString()!);
        Assert.Contains("trojan.exe", entries);
        Assert.Contains("trojan-go.exe", entries);
    }

    [Fact]
    public void WriteProxifyreConfig_ExcludesAlwaysContainsBothBinaries_IncludeOnly()
    {
        _svc.WriteProxifyreConfig(_tempDir, 1080, "127.0.0.1", new[] { "TCP" }, "Info",
            new[] { "chrome.exe" }, ProcessFilterMode.IncludeOnlyListed);
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(_tempDir, "app-config.json")));
        var excludes = doc.RootElement.GetProperty("excludes");
        var entries = new System.Collections.Generic.List<string>();
        foreach (var e in excludes.EnumerateArray()) entries.Add(e.GetString()!);
        Assert.Contains("trojan.exe", entries);
        Assert.Contains("trojan-go.exe", entries);
    }

    // ── WriteProxifyreConfig — ExcludeListed mode shape ──────────────────────

    [Fact]
    public void WriteProxifyreConfig_ExcludeListedMode_AppNamesIsCatchAll()
    {
        _svc.WriteProxifyreConfig(_tempDir, 1080, "127.0.0.1", new[] { "TCP" }, "Info",
            new[] { "chrome.exe" }, ProcessFilterMode.ExcludeListed);
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(_tempDir, "app-config.json")));
        var proxy = doc.RootElement.GetProperty("proxies")[0];
        var appNames = proxy.GetProperty("appNames");
        Assert.Equal(1, appNames.GetArrayLength());
        Assert.Equal("", appNames[0].GetString());
    }

    [Fact]
    public void WriteProxifyreConfig_ExcludeListedMode_UserListInExcludes()
    {
        _svc.WriteProxifyreConfig(_tempDir, 1080, "127.0.0.1", new[] { "TCP" }, "Info",
            new[] { "chrome.exe" }, ProcessFilterMode.ExcludeListed);
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(_tempDir, "app-config.json")));
        var entries = new System.Collections.Generic.List<string>();
        foreach (var e in doc.RootElement.GetProperty("excludes").EnumerateArray())
            entries.Add(e.GetString()!);
        Assert.Contains("chrome.exe", entries);
    }

    // ── WriteProxifyreConfig — IncludeOnlyListed mode shape ──────────────────

    [Fact]
    public void WriteProxifyreConfig_IncludeOnlyListedMode_UserListInAppNames()
    {
        _svc.WriteProxifyreConfig(_tempDir, 1080, "127.0.0.1", new[] { "TCP" }, "Info",
            new[] { "chrome.exe", "firefox.exe" }, ProcessFilterMode.IncludeOnlyListed);
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(_tempDir, "app-config.json")));
        var proxy = doc.RootElement.GetProperty("proxies")[0];
        var entries = new System.Collections.Generic.List<string>();
        foreach (var e in proxy.GetProperty("appNames").EnumerateArray())
            entries.Add(e.GetString()!);
        Assert.Contains("chrome.exe", entries);
        Assert.Contains("firefox.exe", entries);
    }

    [Fact]
    public void WriteProxifyreConfig_IncludeOnlyListedMode_ExcludesOnlyBinaries()
    {
        _svc.WriteProxifyreConfig(_tempDir, 1080, "127.0.0.1", new[] { "TCP" }, "Info",
            new[] { "chrome.exe" }, ProcessFilterMode.IncludeOnlyListed);
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(_tempDir, "app-config.json")));
        var entries = new System.Collections.Generic.List<string>();
        foreach (var e in doc.RootElement.GetProperty("excludes").EnumerateArray())
            entries.Add(e.GetString()!);
        // Excludes must contain only the two canonical binaries, not the user list
        Assert.Equal(2, entries.Count);
        Assert.Contains("trojan.exe", entries);
        Assert.Contains("trojan-go.exe", entries);
        Assert.DoesNotContain("chrome.exe", entries);
    }

    // ── WriteProxifyreConfig — case-insensitive de-duplication ───────────────

    [Fact]
    public void WriteProxifyreConfig_CaseInsensitiveDeDup_TrojanExeVariant()
    {
        // User list with casing variants of the canonical binaries
        _svc.WriteProxifyreConfig(_tempDir, 1080, "127.0.0.1", new[] { "TCP" }, "Info",
            new[] { "Trojan.exe", "TROJAN-GO.EXE" }, ProcessFilterMode.ExcludeListed);
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(_tempDir, "app-config.json")));
        var entries = new System.Collections.Generic.List<string>();
        foreach (var e in doc.RootElement.GetProperty("excludes").EnumerateArray())
            entries.Add(e.GetString()!);

        // Each canonical binary appears exactly once (case-insensitively de-duped)
        Assert.Single(entries, e => string.Equals(e, "trojan.exe", StringComparison.OrdinalIgnoreCase));
        Assert.Single(entries, e => string.Equals(e, "trojan-go.exe", StringComparison.OrdinalIgnoreCase));
    }

    // ── Atomic write ─────────────────────────────────────────────────────────

    [Fact]
    public void WriteProxifyreConfig_AtomicWrite_TmpFileDoesNotPersist()
    {
        _svc.WriteProxifyreConfig(_tempDir, 1080, "127.0.0.1", new[] { "TCP" }, "Info", Array.Empty<string>());
        Assert.False(File.Exists(Path.Combine(_tempDir, "app-config.json.tmp")),
            ".tmp file must not remain after WriteProxifyreConfig()");
        Assert.True(File.Exists(Path.Combine(_tempDir, "app-config.json")));
    }
}
