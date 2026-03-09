using System;
using System.IO;
using System.Linq;
using Avalonia.Headless.XUnit;
using trojan4win.Services;
using trojan4win.ViewModels;
using Xunit;

namespace trojan4win.Tests;

// Each test gets a fresh temp dir for SettingsService file I/O (same pattern as
// SettingsServiceTests). MainViewModel is created inside each [AvaloniaFact] so it
// runs on the Avalonia UI thread, where DispatcherTimer is valid.
public sealed class MainViewModelCommandTests : IDisposable
{
    private readonly string _tempDir;

    public MainViewModelCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "trojan4win_vm_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        SettingsService._testSettingsDir = _tempDir;
    }

    public void Dispose()
    {
        SettingsService._testSettingsDir = null;
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── AddServer ─────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void AddServer_AddsOneServerToCollection()
    {
        using var vm = new MainViewModel();
        vm.AddServerCommand.Execute(null);
        Assert.Single(vm.Servers);
    }

    [AvaloniaFact]
    public void AddServer_NewServerHasDefaultName()
    {
        using var vm = new MainViewModel();
        vm.AddServerCommand.Execute(null);
        Assert.Equal("New Server", vm.Servers[0].Name);
    }

    // ── RemoveServer ──────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void RemoveServer_AfterAdd_CollectionIsEmpty()
    {
        using var vm = new MainViewModel();
        vm.AddServerCommand.Execute(null);
        vm.RemoveServerCommand.Execute(null);
        Assert.Empty(vm.Servers);
    }

    // ── CloneServer (DuplicateServer) ─────────────────────────────────────────

    [AvaloniaFact]
    public void CloneServer_ProducesSecondServerWithDifferentId()
    {
        using var vm = new MainViewModel();
        vm.AddServerCommand.Execute(null);
        var originalId = vm.Servers[0].Id;
        vm.DuplicateServerCommand.Execute(null);
        Assert.Equal(2, vm.Servers.Count);
        Assert.NotEqual(originalId, vm.Servers[1].Id);
    }

    [AvaloniaFact]
    public void CloneServer_CloneNameContainsOriginalName()
    {
        using var vm = new MainViewModel();
        vm.AddServerCommand.Execute(null);
        var originalName = vm.Servers[0].Name;
        vm.DuplicateServerCommand.Execute(null);
        Assert.Contains(originalName, vm.Servers[1].Name);
    }

    [AvaloniaFact]
    public void CloneServer_ClonePreservesRemoteAddr()
    {
        using var vm = new MainViewModel();
        vm.AddServerCommand.Execute(null);
        vm.Servers[0].RemoteAddr = "example.com";
        vm.DuplicateServerCommand.Execute(null);
        Assert.Equal("example.com", vm.Servers[1].RemoteAddr);
    }

    // ── AddExcludedProcess ────────────────────────────────────────────────────

    [AvaloniaFact]
    public void AddExcludedProcess_AppearsInCollection()
    {
        using var vm = new MainViewModel();
        vm.NewExcludedProcess = "chrome.exe";
        vm.AddExcludedProcessCommand.Execute(null);
        Assert.Contains("chrome.exe", vm.ExcludedProcesses);
    }

    [AvaloniaFact]
    public void AddExcludedProcess_Duplicate_OnlyOneEntry()
    {
        using var vm = new MainViewModel();
        vm.NewExcludedProcess = "chrome.exe";
        vm.AddExcludedProcessCommand.Execute(null);
        vm.NewExcludedProcess = "chrome.exe";
        vm.AddExcludedProcessCommand.Execute(null);
        Assert.Single(vm.ExcludedProcesses.Where(p => p == "chrome.exe"));
    }

    // ── RemoveExcludedProcess ─────────────────────────────────────────────────

    [AvaloniaFact]
    public void RemoveExcludedProcess_AfterAdd_CollectionIsEmpty()
    {
        using var vm = new MainViewModel();
        vm.NewExcludedProcess = "chrome.exe";
        vm.AddExcludedProcessCommand.Execute(null);
        vm.RemoveExcludedProcessCommand.Execute("chrome.exe");
        Assert.Empty(vm.ExcludedProcesses);
    }
}
