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
}
