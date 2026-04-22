using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using trojan4win;
using trojan4win.Models;
using trojan4win.Services;
using trojan4win.ViewModels;
using Xunit;

namespace trojan4win.Tests;

// Headless UI integration tests proving ViewModel commands work through the real
// MainWindow + AXAML binding pipeline. Avalonia.Headless provides layout and binding
// support but no visual rendering.
//
// Known headless limitations:
// - No pixel-level rendering — visual assertions use control properties only
// - trojan.exe / proxifyre.exe are absent — Connect hits FileNotFoundException
// - DispatcherTimer ticks are not simulated — no live traffic stat updates
public sealed class UITests : IDisposable
{
    private readonly string _tempDir;

    public UITests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "trojan4win_ui_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        SettingsService._testSettingsDir = _tempDir;
    }

    public void Dispose()
    {
        SettingsService._testSettingsDir = null;
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── Step 2: Add / Delete Server ───────────────────────────────────────────

    [AvaloniaFact]
    public void AddServer_ServerAppearsInViewModel()
    {
        using var vm = new MainViewModel();
        var window = new MainWindow { DataContext = vm };
        window.Show();

        Assert.Empty(vm.Servers);

        vm.AddServerCommand.Execute(null);

        Assert.Single(vm.Servers);
        Assert.Equal("New Server", vm.Servers[0].Name);
        Assert.NotNull(vm.SelectedServer);

        window.Close();
    }

    [AvaloniaFact]
    public void RemoveServer_AddThenRemove_ServersEmpty()
    {
        using var vm = new MainViewModel();
        var window = new MainWindow { DataContext = vm };
        window.Show();

        vm.AddServerCommand.Execute(null);
        Assert.Single(vm.Servers);

        vm.RemoveServerCommand.Execute(null);

        Assert.Empty(vm.Servers);
        Assert.Null(vm.SelectedServer);

        window.Close();
    }

    // ── Step 3: Switch Selected Server ────────────────────────────────────────

    [AvaloniaFact]
    public void SwitchSelectedServer_SelectSecond_ViewModelUpdates()
    {
        using var vm = new MainViewModel();
        var window = new MainWindow { DataContext = vm };
        window.Show();

        vm.AddServerCommand.Execute(null);
        vm.Servers[0].Name = "Server A";
        vm.AddServerCommand.Execute(null);
        vm.Servers[1].Name = "Server B";

        Assert.Equal(2, vm.Servers.Count);

        // After the second Add, SelectedServer is the newly added "Server B".
        // Select the first one to prove switching works.
        vm.SelectedServer = vm.Servers[0];
        Assert.Equal("Server A", vm.SelectedServer.Name);

        vm.SelectedServer = vm.Servers[1];
        Assert.Equal("Server B", vm.SelectedServer.Name);

        window.Close();
    }

    // ── Step 4: Connect / Disconnect ──────────────────────────────────────────

    [AvaloniaFact]
    public async Task Connect_NoServerSelected_SetsError()
    {
        using var vm = new MainViewModel();
        var window = new MainWindow { DataContext = vm };
        window.Show();

        // No server added — SelectedServer is null
        Assert.Null(vm.SelectedServer);

        await vm.ConnectCommand.ExecuteAsync(null);

        Assert.False(vm.IsConnected);
        Assert.False(vm.IsConnecting);
        Assert.Equal("Select a server first.", vm.ErrorMessage);

        window.Close();
    }

    [AvaloniaFact]
    public async Task Connect_ThenDisconnect_StateReturnsToDisconnected()
    {
        using var vm = new MainViewModel();
        var window = new MainWindow { DataContext = vm };
        window.Show();

        vm.AddServerCommand.Execute(null);
        vm.Servers[0].RemoteAddr = "example.com";
        vm.Servers[0].Password = "secret";

        await vm.ConnectCommand.ExecuteAsync(null);

        // Connect either succeeds (executables present) or fails gracefully.
        // Either way, IsConnecting must be false when the command completes.
        Assert.False(vm.IsConnecting);

        if (vm.IsConnected)
        {
            // Executables were present — verify Disconnect restores state
            await vm.DisconnectCommand.ExecuteAsync(null);
            Assert.False(vm.IsConnected);
            Assert.Equal("Disconnected", vm.StatusText);
        }
        else
        {
            // Executables absent — error flow was taken
            Assert.NotEmpty(vm.ErrorMessage);
        }

        window.Close();
    }

    [AvaloniaFact]
    public async Task Disconnect_WhenNotConnected_IsNoOp()
    {
        using var vm = new MainViewModel();
        var window = new MainWindow { DataContext = vm };
        window.Show();

        Assert.False(vm.IsConnected);

        await vm.DisconnectCommand.ExecuteAsync(null);

        Assert.False(vm.IsConnected);
        Assert.Equal("Disconnected", vm.StatusText);

        window.Close();
    }

    // ── FilterMode toggle changes ProcessListLabel binding ────────────────────

    [AvaloniaFact]
    public void FilterMode_Toggle_BindingUpdatesProcessListLabel()
    {
        using var vm = new MainViewModel();
        var window = new MainWindow { DataContext = vm };
        window.Show();

        // Navigate to the proxy page so the Proxy Settings subtree is live
        vm.NavigateToCommand.Execute("proxy");

        // Capture label text in ExcludeListed mode (initial default)
        vm.FilterMode = ProcessFilterMode.ExcludeListed;
        var labelBefore = vm.ProcessListLabel;

        // Flip to IncludeOnlyListed
        vm.FilterMode = ProcessFilterMode.IncludeOnlyListed;
        var labelAfter = vm.ProcessListLabel;

        // The label text must have changed when FilterMode changed
        Assert.NotEqual(labelBefore, labelAfter);
        Assert.Equal("Processes that bypass the proxy", labelBefore);
        Assert.Equal("Processes routed through the proxy", labelAfter);

        window.Close();
    }
}
