using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using trojan4win.ViewModels;

namespace trojan4win;

public partial class App : Application
{
    private MainViewModel? _vm;
    private TrayIcon? _trayIcon;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _vm = new MainViewModel();
            var mainWindow = new MainWindow { DataContext = _vm };
            desktop.MainWindow = mainWindow;

            desktop.ShutdownRequested += (_, _) =>
            {
                _vm?.Dispose();
                _trayIcon?.Dispose();
            };

            SetupTrayIcon(mainWindow);
            SubscribeToTrayTooltipUpdates();

            var startMinimized = desktop.Args?.Contains("--minimized") == true;
            if (startMinimized && _vm.MinimizeToTray)
                mainWindow.Hide();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon(Window mainWindow)
    {
        _trayIcon = new TrayIcon
        {
            ToolTipText = "trojan4win",
            IsVisible = true,
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://trojan4win/app.ico"))),
        };

        var showItem = new NativeMenuItem("Show");
        showItem.Click += (_, _) =>
        {
            mainWindow.Show();
            mainWindow.WindowState = WindowState.Normal;
            mainWindow.Activate();
        };

        var exitItem = new NativeMenuItem("Exit");
        exitItem.Click += (_, _) =>
        {
            _vm?.Dispose();
            _trayIcon?.Dispose();
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown();
        };

        var menu = new NativeMenu();
        menu.Items.Add(showItem);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(exitItem);

        _trayIcon.Menu = menu;
        _trayIcon.Clicked += (_, _) =>
        {
            mainWindow.Show();
            mainWindow.WindowState = WindowState.Normal;
            mainWindow.Activate();
        };
    }

    private void SubscribeToTrayTooltipUpdates()
    {
        if (_vm == null) return;

        _vm.PropertyChanged += OnVmPropertyChanged;
        UpdateTrayTooltip();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.IsConnected):
            case nameof(MainViewModel.StatusText):
            case nameof(MainViewModel.SessionDuration):
            case nameof(MainViewModel.SessionBytesUp):
            case nameof(MainViewModel.SessionBytesDown):
            case nameof(MainViewModel.SpeedUp):
            case nameof(MainViewModel.SpeedDown):
            case nameof(MainViewModel.SelectedServer):
                UpdateTrayTooltip();
                break;
        }
    }

    private void UpdateTrayTooltip()
    {
        if (_trayIcon == null || _vm == null) return;

        if (!_vm.IsConnected)
        {
            _trayIcon.ToolTipText = "trojan4win - Disconnected";
            return;
        }

        var server = _vm.SelectedServer?.Name ?? "Unknown";
        var up = MainViewModel.FormatBytes(_vm.SessionBytesUp);
        var down = MainViewModel.FormatBytes(_vm.SessionBytesDown);
        var speedUp = MainViewModel.FormatSpeed(_vm.SpeedUp);
        var speedDown = MainViewModel.FormatSpeed(_vm.SpeedDown);

        _trayIcon.ToolTipText =
            $"trojan4win - Connected\n" +
            $"Server: {server}\n" +
            $"Duration: {_vm.SessionDuration}\n" +
            $"Up: {up} ({speedUp})  Down: {down} ({speedDown})";
    }
}