using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using trojan4win.Models;
using trojan4win.Services;

namespace trojan4win.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly TrojanService _trojan = new();
    private readonly ProxifyreService _proxifyre = new();
    private readonly TrafficMonitor _trafficMonitor = new();
    private AppSettings _settings;
    private CancellationTokenSource? _connectCts;
    private DispatcherTimer? _autoPingTimer;
    private bool _initialized;
    // CR-11: stored delegates allow unsubscription in Dispose() to prevent
    // post-dispose callbacks dispatching to a dead ViewModel
    private readonly Action<string> _onTrojanLog;
    private readonly Action<string> _onProxifyreLog;
    private readonly Action _onTrojanExited;
    private readonly Action _onProxifyreExited;
    private readonly Action _onTrafficUpdated;
    private readonly EventHandler _onAutoPingTick;  // CR-21: stored for Dispose() unsubscription

    public MainViewModel()
    {
        _settings = SettingsService.Load();

        foreach (var s in _settings.Servers)
            Servers.Add(s);

        FilteredProcesses = new ObservableCollection<string>(_settings.FilteredProcesses);
        LocalSocksPort = _settings.LocalSocksPort;
        LocalAddr = _settings.LocalAddr;
        ProxySupportTcp = _settings.SupportedProtocols.Contains("TCP");
        ProxySupportUdp = _settings.SupportedProtocols.Contains("UDP");
        ProxyLogLevel = _settings.ProxyLogLevel;
        AutoStart = _settings.AutoStart;
        AutoConnect = _settings.AutoConnect;
        MinimizeToTray = _settings.MinimizeToTray;
        _filterMode = _settings.FilterMode;

        if (_settings.LastSelectedServerId != null)
            SelectedServer = Servers.FirstOrDefault(s => s.Id == _settings.LastSelectedServerId);
        SelectedServer ??= Servers.FirstOrDefault();

        // CR-11: assign to named delegates so Dispose() can unsubscribe them
        _onTrojanLog = line => Dispatcher.UIThread.Post(() =>
        {
            TrojanLog += line + "\n";
            if (TrojanLog.Length > 100_000)
                TrojanLog = TrojanLog[^50_000..];
        });
        _trojan.LogReceived += _onTrojanLog;

        _onProxifyreLog = line => Dispatcher.UIThread.Post(() =>
        {
            ProxifyreLog += line + "\n";
            if (ProxifyreLog.Length > 100_000)
                ProxifyreLog = ProxifyreLog[^50_000..];
        });
        _proxifyre.LogReceived += _onProxifyreLog;

        // CR-12: OnProcessExited is async void — exceptions surface on the UI
        // SynchronizationContext instead of being silently swallowed
        _onTrojanExited = () => Dispatcher.UIThread.Post(OnProcessExited);
        _trojan.ProcessExited += _onTrojanExited;

        _onProxifyreExited = () => Dispatcher.UIThread.Post(OnProcessExited);
        _proxifyre.ProcessExited += _onProxifyreExited;

        _onTrafficUpdated = () => Dispatcher.UIThread.Post(() =>
        {
            SessionBytesUp = _trafficMonitor.SessionBytesUp;
            SessionBytesDown = _trafficMonitor.SessionBytesDown;
            SpeedUp = _trafficMonitor.SpeedUp;
            SpeedDown = _trafficMonitor.SpeedDown;
            SessionDuration = FormatDuration(_trafficMonitor.SessionDuration);
        });
        _trafficMonitor.Updated += _onTrafficUpdated;

        CurrentPage = "home";

        _autoPingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _onAutoPingTick = async (_, _) => await AutoPingAllServersAsync();  // CR-21
        _autoPingTimer.Tick += _onAutoPingTick;
        _autoPingTimer.Start();
        _ = AutoPingAllServersAsync();
        _initialized = true;
    }

    // --- Connection State ---
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _isConnecting;
    [ObservableProperty] private string _statusText = "Disconnected";

    // --- Traffic Stats ---
    [ObservableProperty] private long _sessionBytesUp;
    [ObservableProperty] private long _sessionBytesDown;
    [ObservableProperty] private long _speedUp;
    [ObservableProperty] private long _speedDown;
    [ObservableProperty] private string _sessionDuration = "00:00:00";
    [ObservableProperty] private long _totalBytesUp;
    [ObservableProperty] private long _totalBytesDown;
    [ObservableProperty] private long _totalSeconds;

    // --- Servers ---
    public ObservableCollection<ServerConfig> Servers { get; } = new();
    [ObservableProperty] private ServerConfig? _selectedServer;

    // --- Editor ---
    [ObservableProperty] private ServerConfig? _editingServer;
    [ObservableProperty] private string _editName = "";
    [ObservableProperty] private string _editRegion = "";
    [ObservableProperty] private string _editRemoteAddr = "";
    [ObservableProperty] private int _editRemotePort = 443;
    [ObservableProperty] private string _editPassword = "";
    [ObservableProperty] private bool _editVerifyCert = true;
    [ObservableProperty] private string _editSni = "";
    [ObservableProperty] private string _editAlpn = "h2,http/1.1";
    [ObservableProperty] private bool _isPasswordVisible;
    [ObservableProperty] private int _editTrojanLogLevel = 1;
    [ObservableProperty] private string _editCert = "";
    [ObservableProperty] private string _editKey = "";
    [ObservableProperty] private string _editCurves = "";
    [ObservableProperty] private bool _editNoDelay = true;
    [ObservableProperty] private bool _editKeepAlive = true;

    // --- Settings ---
    [ObservableProperty] private int _localSocksPort = 1080;
    [ObservableProperty] private string _localAddr = "127.0.0.1";
    [ObservableProperty] private bool _proxySupportTcp = true;
    [ObservableProperty] private bool _proxySupportUdp = true;
    [ObservableProperty] private string _proxyLogLevel = "Info";
    [ObservableProperty] private bool _autoStart;
    [ObservableProperty] private bool _autoConnect;
    [ObservableProperty] private bool _minimizeToTray = true;
    public ObservableCollection<string> FilteredProcesses { get; set; }
    [ObservableProperty] private string _newFilteredProcess = "";
    [ObservableProperty] private ProcessFilterMode _filterMode = ProcessFilterMode.ExcludeListed;

    public string ProcessListLabel => FilterMode == ProcessFilterMode.ExcludeListed
        ? "Processes that bypass the proxy"
        : "Processes routed through the proxy";

    public string ProcessListHelpText => FilterMode == ProcessFilterMode.ExcludeListed
        ? "Traffic from these processes will NOT be redirected through the SOCKS proxy."
        : "Only traffic from these processes will be redirected through the SOCKS proxy.";

    // --- ComboBox Sources ---
    public List<string> TrojanLogLevelOptions { get; } = new() { "All", "Info", "Warn", "Error", "Fatal", "Off" };
    public List<string> ProxyLogLevels { get; } = new() { "Error", "Warning", "Info", "Debug", "All" };

    // --- Logs ---
    [ObservableProperty] private string _trojanLog = "";
    [ObservableProperty] private string _proxifyreLog = "";

    // --- Navigation ---
    [ObservableProperty] private string _currentPage = "home";

    // --- Error Message ---
    [ObservableProperty] private string _errorMessage = "";

    partial void OnSelectedServerChanged(ServerConfig? value)
    {
        if (value != null)
        {
            _settings.LastSelectedServerId = value.Id;
            SaveSettings();
            if (_initialized)
                _ = RefreshPingAsync();
            LoadStatsForServer(value.Id);
        }
        else
        {
            TotalBytesUp = 0;
            TotalBytesDown = 0;
            TotalSeconds = 0;
        }
    }

    partial void OnFilterModeChanged(ProcessFilterMode value)
    {
        _settings.FilterMode = value;
        SaveSettings();
        OnPropertyChanged(nameof(ProcessListLabel));
        OnPropertyChanged(nameof(ProcessListHelpText));
    }

    partial void OnAutoStartChanged(bool value)
    {
        _settings.AutoStart = value;
        AutoStartService.SetAutoStart(value);
        SaveSettings();
    }

    partial void OnAutoConnectChanged(bool value)
    {
        _settings.AutoConnect = value;
        SaveSettings();
    }

    partial void OnMinimizeToTrayChanged(bool value)
    {
        _settings.MinimizeToTray = value;
        SaveSettings();
    }

    partial void OnLocalSocksPortChanged(int value)
    {
        _settings.LocalSocksPort = value;
        SaveSettings();
    }

    partial void OnLocalAddrChanged(string value)
    {
        _settings.LocalAddr = value;
        SaveSettings();
    }

    partial void OnProxySupportTcpChanged(bool value) => UpdateSupportedProtocols();
    partial void OnProxySupportUdpChanged(bool value) => UpdateSupportedProtocols();

    partial void OnProxyLogLevelChanged(string value)
    {
        _settings.ProxyLogLevel = value;
        SaveSettings();
    }

    private void UpdateSupportedProtocols()
    {
        var protocols = new List<string>();
        if (ProxySupportTcp) protocols.Add("TCP");
        if (ProxySupportUdp) protocols.Add("UDP");
        _settings.SupportedProtocols = protocols;
        SaveSettings();
    }

    // --- Navigation Commands ---
    [RelayCommand]
    private void NavigateTo(string page) => CurrentPage = page;

    // --- Connect / Disconnect ---
    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (SelectedServer == null)
        {
            ErrorMessage = "Select a server first.";
            return;
        }

        ErrorMessage = "";
        IsConnecting = true;
        StatusText = "Connecting...";

        try
        {
            _connectCts?.Dispose();  // CR-19: release previous CTS's WaitHandle on reconnect
            _connectCts = new CancellationTokenSource();
            TrojanLog = "";
            ProxifyreLog = "";

            await _trojan.StartAsync(SelectedServer, LocalSocksPort, LocalAddr, _connectCts.Token);

            if (!_trojan.IsRunning)
            {
                ErrorMessage = "Failed to start trojan process.";
                StatusText = "Disconnected";
                IsConnecting = false;
                return;
            }

            var protocols = new List<string>();
            if (ProxySupportTcp) protocols.Add("TCP");
            if (ProxySupportUdp) protocols.Add("UDP");
            await _proxifyre.StartAsync(LocalSocksPort, LocalAddr, protocols, ProxyLogLevel, FilteredProcesses.ToList(), _settings.FilterMode, _connectCts.Token);

            _trafficMonitor.Start();

            IsConnected = true;
            StatusText = $"Connected to {SelectedServer.Name}";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusText = "Disconnected";
            IsConnected = false;  // CR-20: explicit reset; benign today, guards future reordering
            _trojan.Stop();
            _proxifyre.Stop();
        }
        finally
        {
            IsConnecting = false;
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        _connectCts?.Cancel();
        _trafficMonitor.Stop();

        var serverId = SelectedServer?.Id;
        if (serverId != null)
        {
            if (!_settings.ServerStats.TryGetValue(serverId, out var stats))
            {
                stats = new UsageStats();
                _settings.ServerStats[serverId] = stats;
            }
            stats.TotalBytesUp += SessionBytesUp;
            stats.TotalBytesDown += SessionBytesDown;
            stats.TotalSeconds += (long)_trafficMonitor.SessionDuration.TotalSeconds;

            TotalBytesUp = stats.TotalBytesUp;
            TotalBytesDown = stats.TotalBytesDown;
            TotalSeconds = stats.TotalSeconds;
        }
        SaveSettings();

        _proxifyre.Stop();
        _trojan.Stop();

        IsConnected = false;
        StatusText = "Disconnected";
        SessionBytesUp = 0;
        SessionBytesDown = 0;
        SpeedUp = 0;
        SpeedDown = 0;
        SessionDuration = "00:00:00";
    }

    [RelayCommand]
    private void ResetStats()
    {
        var serverId = SelectedServer?.Id;
        if (serverId != null)
            _settings.ServerStats[serverId] = new UsageStats();
        TotalBytesUp = 0;
        TotalBytesDown = 0;
        TotalSeconds = 0;
        SaveSettings();
    }

    // --- Server Management ---
    [RelayCommand]
    private void AddServer()
    {
        var server = new ServerConfig();
        Servers.Add(server);
        SelectedServer = server;
        StartEditing(server);
        CurrentPage = "edit";
        SaveSettings();
    }

    [RelayCommand]
    private void DuplicateServer()
    {
        if (SelectedServer == null) return;
        var clone = SelectedServer.Clone();
        Servers.Add(clone);
        SelectedServer = clone;
        SaveSettings();
    }

    [RelayCommand]
    private void RemoveServer()
    {
        if (SelectedServer == null) return;
        var idx = Servers.IndexOf(SelectedServer);
        Servers.Remove(SelectedServer);
        SelectedServer = Servers.Count > 0 ? Servers[Math.Min(idx, Servers.Count - 1)] : null;
        SaveSettings();
    }

    [RelayCommand]
    private void EditServer()
    {
        if (SelectedServer == null) return;
        StartEditing(SelectedServer);
        CurrentPage = "edit";
    }

    private void StartEditing(ServerConfig s)
    {
        EditingServer = s;
        EditName = s.Name;
        EditRegion = s.Region;
        EditRemoteAddr = s.RemoteAddr;
        EditRemotePort = s.RemotePort;
        EditPassword = s.Password;
        EditVerifyCert = s.VerifyCert;
        EditSni = s.Sni;
        EditAlpn = s.Alpn;
        IsPasswordVisible = false;
        EditTrojanLogLevel = s.TrojanLogLevel;
        EditCert = s.Cert;
        EditKey = s.Key;
        EditCurves = s.Curves;
        EditNoDelay = s.NoDelay;
        EditKeepAlive = s.KeepAlive;
    }

    [RelayCommand]
    private void SaveServer()
    {
        if (EditingServer == null) return;
        EditingServer.Name = EditName;
        EditingServer.Region = EditRegion;
        EditingServer.RemoteAddr = EditRemoteAddr;
        EditingServer.RemotePort = EditRemotePort;
        EditingServer.Password = EditPassword;
        EditingServer.VerifyCert = EditVerifyCert;
        EditingServer.Sni = EditSni;
        EditingServer.Alpn = EditAlpn;
        EditingServer.TrojanLogLevel = EditTrojanLogLevel;
        EditingServer.Cert = EditCert;
        EditingServer.Key = EditKey;
        EditingServer.Curves = EditCurves;
        EditingServer.NoDelay = EditNoDelay;
        EditingServer.KeepAlive = EditKeepAlive;

        var idx = Servers.IndexOf(EditingServer);
        if (idx >= 0)
        {
            Servers[idx] = EditingServer;
            SelectedServer = EditingServer;
        }

        SaveSettings();
        CurrentPage = "home";
    }

    [RelayCommand]
    private void CancelEdit()
    {
        CurrentPage = "home";
    }

    [RelayCommand]
    private void TogglePasswordVisibility()
    {
        IsPasswordVisible = !IsPasswordVisible;
    }

    // --- Router Rules (editor) ---
    [ObservableProperty] private RouterRule? _selectedRouterRule;

    [RelayCommand]
    private void AddRouterRule()
    {
        if (EditingServer == null) return;
        var rule = new RouterRule();
        EditingServer.RouterRules.Add(rule);
        SelectedRouterRule = rule;
    }

    [RelayCommand]
    private void RemoveRouterRule()
    {
        if (EditingServer == null || SelectedRouterRule == null) return;
        EditingServer.RouterRules.Remove(SelectedRouterRule);
        SelectedRouterRule = null;
    }

    [RelayCommand]
    private async Task BrowseGeoipAsync() => await BrowseGeoFileAsync(isGeoip: true);

    [RelayCommand]
    private async Task BrowseGeositeAsync() => await BrowseGeoFileAsync(isGeoip: false);

    private async Task BrowseGeoFileAsync(bool isGeoip)
    {
        if (EditingServer == null) return;
        try
        {
            var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var mainWindow = lifetime?.MainWindow;
            if (mainWindow == null) return;

            var files = await mainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = isGeoip ? "Select GeoIP file" : "Select GeoSite file",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("DAT Files") { Patterns = new[] { "*.dat" } }
                }
            });

            if (files.Count == 0) return;
            var path = files[0].Path.LocalPath;
            if (isGeoip) EditingServer.RouterGeoip = path;
            else EditingServer.RouterGeosite = path;
        }
        catch { /* ignore picker errors */ }
    }

    // --- Router / Shadowsocks / Fingerprint combobox sources ---
    public List<string> RouterPolicyOptions { get; } = new() { "bypass", "proxy", "block" };
    public List<string> RouterRuleTypeOptions { get; } = new() { "domain", "full", "regexp", "cidr", "geoip", "geosite" };
    public List<string> RouterDomainStrategyOptions { get; } = new() { "as_is", "ip_if_non_match", "ip_on_demand" };
    public List<string> ShadowsocksMethodOptions { get; } = new() { "AES-128-GCM", "AES-256-GCM", "CHACHA20-IETF-POLY1305" };
    public List<string> FingerprintOptions { get; } = new() { "", "firefox", "chrome", "ios" };
    public List<int> MuxProtocolOptions { get; } = new() { 1, 2 };

    [RelayCommand]
    private async Task ImportConfigAsync()
    {
        try
        {
            var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var mainWindow = lifetime?.MainWindow;
            if (mainWindow == null) return;

            var files = await mainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import Server Config (JSON)",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } }
                }
            });

            if (files.Count == 0) return;

            var path = files[0].Path.LocalPath;
            var json = await File.ReadAllTextAsync(path);

            var imported = JsonSerializer.Deserialize<ServerConfig>(json);
            if (imported != null)
            {
                imported.Id = Guid.NewGuid().ToString();
                if (string.IsNullOrWhiteSpace(imported.Name))
                    imported.Name = Path.GetFileNameWithoutExtension(path);
                Servers.Add(imported);
                SelectedServer = imported;
                SaveSettings();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Import failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ExportServerAsync()
    {
        if (SelectedServer == null) return;

        try
        {
            var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var mainWindow = lifetime?.MainWindow;
            if (mainWindow == null) return;

            var suggestedName = string.IsNullOrWhiteSpace(SelectedServer.Name)
                ? "server"
                : SelectedServer.Name;

            var file = await mainWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Server Config",
                SuggestedFileName = suggestedName + ".json",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } }
                }
            });

            if (file == null) return;

            var json = JsonSerializer.Serialize(SelectedServer, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(file.Path.LocalPath, json);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Export failed: {ex.Message}";
        }
    }

    // --- Proxy Filtering ---
    [RelayCommand]
    private void AddFilteredProcess()
    {
        var name = NewFilteredProcess?.Trim();
        if (string.IsNullOrWhiteSpace(name) || FilteredProcesses.Contains(name)) return;
        FilteredProcesses.Add(name);
        NewFilteredProcess = "";
        _settings.FilteredProcesses = FilteredProcesses.ToList();
        SaveSettings();
    }

    [RelayCommand]
    private void RemoveFilteredProcess(string name)
    {
        FilteredProcesses.Remove(name);
        _settings.FilteredProcesses = FilteredProcesses.ToList();
        SaveSettings();
    }

    // --- Logs ---
    [RelayCommand]
    private void ClearTrojanLogs()
    {
        _trojan.ClearLogs();
        TrojanLog = "";
    }

    [RelayCommand]
    private void ClearProxifyreLogs()
    {
        _proxifyre.ClearLogs();
        ProxifyreLog = "";
    }

    // --- Ping ---
    [RelayCommand]
    private async Task RefreshPingAsync()
    {
        if (SelectedServer == null) return;
        var host = SelectedServer.RemoteAddr;
        if (!string.IsNullOrWhiteSpace(host))
        {
            var ping = await PingService.MeasurePingAsync(host);
            SelectedServer.Ping = ping;
        }
    }

    // --- Auto Connect on Start ---
    public async Task AutoConnectIfNeededAsync()
    {
        if (AutoConnect && SelectedServer != null)
            await ConnectAsync();
    }

    // CR-12: async void is intentional for event-handler context; exceptions are
    // surfaced via ErrorMessage rather than silently discarded by "_ = Task"
    private async void OnProcessExited()
    {
        if (!IsConnected) return;
        try { await DisconnectAsync(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    // --- Helpers ---
    private void SaveSettings()
    {
        _settings.Servers = Servers.ToList();
        _settings.FilteredProcesses = FilteredProcesses.ToList();
        _settings.LocalSocksPort = LocalSocksPort;
        _settings.LocalAddr = LocalAddr;
        _settings.ProxyLogLevel = ProxyLogLevel;
        var protocols = new List<string>();
        if (ProxySupportTcp) protocols.Add("TCP");
        if (ProxySupportUdp) protocols.Add("UDP");
        _settings.SupportedProtocols = protocols;
        SettingsService.Save(_settings);
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }

    public static string FormatSpeed(long bytesPerSec)
    {
        return FormatBytes(bytesPerSec) + "/s";
    }

    public static string FormatDuration(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return ts.ToString(@"hh\:mm\:ss");
        return ts.ToString(@"mm\:ss");
    }

    public static string FormatTotalTime(long totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(totalSeconds);
        if (ts.TotalDays >= 1)
            return $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m";
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        return $"{ts.Minutes}m {ts.Seconds}s";
    }

    private void LoadStatsForServer(string serverId)
    {
        if (_settings.ServerStats.TryGetValue(serverId, out var stats))
        {
            TotalBytesUp = stats.TotalBytesUp;
            TotalBytesDown = stats.TotalBytesDown;
            TotalSeconds = stats.TotalSeconds;
        }
        else
        {
            TotalBytesUp = 0;
            TotalBytesDown = 0;
            TotalSeconds = 0;
        }
    }

    private async Task AutoPingAllServersAsync()
    {
        foreach (var server in Servers.ToList())
        {
            if (!string.IsNullOrWhiteSpace(server.RemoteAddr))
            {
                var ping = await PingService.MeasurePingAsync(server.RemoteAddr);
                server.Ping = ping;
            }
        }
    }

    public void Dispose()
    {
        if (_autoPingTimer != null)
        {
            _autoPingTimer.Tick -= _onAutoPingTick;  // CR-21: release delegate reference
            _autoPingTimer.Stop();
        }
        // CR-11: unsubscribe before disposing services so no callbacks reach
        // this ViewModel after it has been disposed
        _trojan.LogReceived -= _onTrojanLog;
        _proxifyre.LogReceived -= _onProxifyreLog;
        _trojan.ProcessExited -= _onTrojanExited;
        _proxifyre.ProcessExited -= _onProxifyreExited;
        _trafficMonitor.Updated -= _onTrafficUpdated;
        _trojan.Dispose();
        _proxifyre.Dispose();
        _trafficMonitor.Dispose();
        GC.SuppressFinalize(this);
    }
}
