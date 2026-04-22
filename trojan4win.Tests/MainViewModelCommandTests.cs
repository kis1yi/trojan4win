using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Headless.XUnit;
using trojan4win.Models;
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

    // ── AddFilteredProcess ────────────────────────────────────────────────────

    [AvaloniaFact]
    public void AddFilteredProcess_AppearsInCollection()
    {
        using var vm = new MainViewModel();
        vm.NewFilteredProcess = "chrome.exe";
        vm.AddFilteredProcessCommand.Execute(null);
        Assert.Contains("chrome.exe", vm.FilteredProcesses);
    }

    [AvaloniaFact]
    public void AddFilteredProcess_Duplicate_OnlyOneEntry()
    {
        using var vm = new MainViewModel();
        vm.NewFilteredProcess = "chrome.exe";
        vm.AddFilteredProcessCommand.Execute(null);
        vm.NewFilteredProcess = "chrome.exe";
        vm.AddFilteredProcessCommand.Execute(null);
        Assert.Single(vm.FilteredProcesses, p => p == "chrome.exe");
    }

    // ── RemoveFilteredProcess ─────────────────────────────────────────────────

    [AvaloniaFact]
    public void RemoveFilteredProcess_AfterAdd_CollectionIsEmpty()
    {
        using var vm = new MainViewModel();
        vm.NewFilteredProcess = "chrome.exe";
        vm.AddFilteredProcessCommand.Execute(null);
        vm.RemoveFilteredProcessCommand.Execute("chrome.exe");
        Assert.Empty(vm.FilteredProcesses);
    }

    // ── FilterMode property change notifications ──────────────────────────────

    [AvaloniaFact]
    public void FilterMode_Change_RaisesPropertyChangedForAllThree()
    {
        using var vm = new MainViewModel();
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != null) raised.Add(e.PropertyName);
        };

        vm.FilterMode = ProcessFilterMode.IncludeOnlyListed;

        Assert.Contains(nameof(vm.FilterMode), raised);
        Assert.Contains(nameof(vm.ProcessListLabel), raised);
        Assert.Contains(nameof(vm.ProcessListHelpText), raised);
    }
}
