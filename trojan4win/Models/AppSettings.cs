using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace trojan4win.Models;

public class AppSettings
{
    public List<ServerConfig> Servers { get; set; } = new();
    public string? LastSelectedServerId { get; set; }
    public bool AutoStart { get; set; }
    public bool AutoConnect { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public int LocalSocksPort { get; set; } = 1080;
    public string LocalAddr { get; set; } = "127.0.0.1";
    public List<string> SupportedProtocols { get; set; } = new() { "TCP", "UDP" };
    public string ProxyLogLevel { get; set; } = "Info";
    [JsonPropertyName("ExcludedProcesses")]
    public List<string> FilteredProcesses { get; set; } = new();
    public ProcessFilterMode FilterMode { get; set; } = ProcessFilterMode.ExcludeListed;
    // CR-22: removed dead Stats property — never read or written; ServerStats (below) is used instead
    public Dictionary<string, UsageStats> ServerStats { get; set; } = new();
}
