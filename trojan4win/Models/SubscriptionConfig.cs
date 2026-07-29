using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using trojan4win.Services;

namespace trojan4win.Models;

public sealed class SubscriptionConfig : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString();
    private string _url = "";
    private bool _allowInsecureConnection;
    private string _userAgent = $"trojan4win/{ApplicationVersion.DisplayVersion}";
    private string? _profileTitle;
    private string? _profileWebPageUrl;
    private string? _announce;
    private long _upload;
    private long _download;
    private long _total;
    private long _expire;
    private DateTimeOffset? _lastUpdatedUtc;
    private int _updateIntervalHours = 1;
    private bool _isCollapsed;
    private Dictionary<string, string> _headers = new(StringComparer.OrdinalIgnoreCase);
    private List<SubscriptionServer> _servers = new();

    public string Id { get => _id; set => Set(ref _id, value); }
    public string Url { get => _url; set => Set(ref _url, value); }
    public bool AllowInsecureConnection { get => _allowInsecureConnection; set => Set(ref _allowInsecureConnection, value); }
    public string UserAgent { get => _userAgent; set => Set(ref _userAgent, value); }
    public string? ProfileTitle { get => _profileTitle; set => Set(ref _profileTitle, value); }
    public string? ProfileWebPageUrl { get => _profileWebPageUrl; set => Set(ref _profileWebPageUrl, value); }
    public string? Announce { get => _announce; set => Set(ref _announce, value); }
    public long Upload { get => _upload; set => Set(ref _upload, value); }
    public long Download { get => _download; set => Set(ref _download, value); }
    public long Total { get => _total; set => Set(ref _total, value); }
    public long Expire { get => _expire; set => Set(ref _expire, value); }
    public DateTimeOffset? LastUpdatedUtc { get => _lastUpdatedUtc; set => Set(ref _lastUpdatedUtc, value); }
    public int UpdateIntervalHours { get => _updateIntervalHours; set => Set(ref _updateIntervalHours, Math.Max(1, value)); }
    public bool IsCollapsed { get => _isCollapsed; set => Set(ref _isCollapsed, value); }
    public Dictionary<string, string> Headers { get => _headers; set => Set(ref _headers, value ?? new(StringComparer.OrdinalIgnoreCase)); }
    public List<SubscriptionServer> Servers { get => _servers; set => Set(ref _servers, value ?? new()); }
    public IEnumerable<ServerConfig> ServerConfigs => Servers.Select(x => x.Server);
    public string DisplayTitle => ProfileTitle ?? "";
    public string LastUpdatedText => LastUpdatedUtc is null
        ? ""
        : $"{LastUpdatedUtc.Value.ToLocalTime():dd.MM.yyyy HH:mm} | Auto-update · {UpdateIntervalHours} h";
    public string TrafficText => Total switch
    {
        -1 => $"{FormatBytes(Upload + Download)} / ∞",
        0 => $"{FormatBytes(Upload + Download)} / 0 GB",
        _ => $"{FormatBytes(Upload + Download)} / {FormatBytes(Total)}"
    };
    public double TrafficPercent => Total > 0
        ? Math.Clamp((Upload + Download) * 100d / Total, 0, 100)
        : 0;
    public string ExpireText => Expire > 0
        ? $"Expires: {DateTimeOffset.FromUnixTimeSeconds(Expire).ToLocalTime():dd.MM.yyyy HH:mm}"
        : "";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        if (name is nameof(Servers)) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ServerConfigs)));
        if (name is nameof(ProfileTitle)) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayTitle)));
        if (name is nameof(LastUpdatedUtc) or nameof(UpdateIntervalHours))
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastUpdatedText)));
        if (name is nameof(Upload) or nameof(Download) or nameof(Total))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TrafficText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TrafficPercent)));
        }
        if (name is nameof(Expire)) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExpireText)));
    }

    private static string FormatBytes(long bytes) =>
        bytes < 1024 * 1024 * 1024
            ? $"{bytes / (1024d * 1024):F1} MB"
            : $"{bytes / (1024d * 1024 * 1024):F2} GB";
}

public sealed class SubscriptionServer
{
    public string SourceKey { get; set; } = "";
    public ServerConfig Server { get; set; } = new();
}
