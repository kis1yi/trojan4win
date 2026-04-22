using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace trojan4win.Models;

public class ServerConfig : INotifyPropertyChanged
{
    // CR-13: all persistent properties raise OnPropertyChanged() so UI bindings
    // update immediately on any direct mutation, not only via SaveServer()'s
    // collection-replacement trick (Servers[idx] = EditingServer)
    private string _id = Guid.NewGuid().ToString();
    public string Id { get => _id; set { _id = value; OnPropertyChanged(); } }

    private string _name = "New Server";
    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }

    private string _region = "";
    public string Region { get => _region; set { _region = value; OnPropertyChanged(); } }

    private string _remoteAddr = "";
    public string RemoteAddr { get => _remoteAddr; set { _remoteAddr = value; OnPropertyChanged(); } }

    private int _remotePort = 443;
    public int RemotePort { get => _remotePort; set { _remotePort = value; OnPropertyChanged(); } }

    private string _password = "";
    public string Password { get => _password; set { _password = value; OnPropertyChanged(); } }

    // ── SSL ──────────────────────────────────────────────────────────────────
    private bool _verifyCert = true;
    public bool VerifyCert { get => _verifyCert; set { _verifyCert = value; OnPropertyChanged(); } }

    private string _sni = "";
    public string Sni { get => _sni; set { _sni = value; OnPropertyChanged(); } }

    private string _alpn = "h2,http/1.1";
    public string Alpn { get => _alpn; set { _alpn = value; OnPropertyChanged(); } }

    private string _cert = "";
    public string Cert { get => _cert; set { _cert = value; OnPropertyChanged(); } }

    private string _key = "";
    public string Key { get => _key; set { _key = value; OnPropertyChanged(); } }

    private string _curves = "";
    public string Curves { get => _curves; set { _curves = value; OnPropertyChanged(); } }

    private string _fingerprint = "";
    public string Fingerprint { get => _fingerprint; set { _fingerprint = value; OnPropertyChanged(); } }

    private bool _ech;
    public bool Ech { get => _ech; set { _ech = value; OnPropertyChanged(); } }

    private string _echConfig = "";
    public string EchConfig { get => _echConfig; set { _echConfig = value; OnPropertyChanged(); } }

    // ── TCP ──────────────────────────────────────────────────────────────────
    private bool _noDelay = true;
    public bool NoDelay { get => _noDelay; set { _noDelay = value; OnPropertyChanged(); } }

    private bool _keepAlive = true;
    public bool KeepAlive { get => _keepAlive; set { _keepAlive = value; OnPropertyChanged(); } }

    private bool _preferIpv4;
    public bool PreferIpv4 { get => _preferIpv4; set { _preferIpv4 = value; OnPropertyChanged(); } }

    // ── Mux ──────────────────────────────────────────────────────────────────
    private bool _muxEnabled;
    public bool MuxEnabled { get => _muxEnabled; set { _muxEnabled = value; OnPropertyChanged(); } }

    private int _muxConcurrency = 8;
    public int MuxConcurrency { get => _muxConcurrency; set { _muxConcurrency = value; OnPropertyChanged(); } }

    private int _muxIdleTimeout = 30;
    public int MuxIdleTimeout { get => _muxIdleTimeout; set { _muxIdleTimeout = value; OnPropertyChanged(); } }

    private int _muxStreamBuffer = 4194304;
    public int MuxStreamBuffer { get => _muxStreamBuffer; set { _muxStreamBuffer = value; OnPropertyChanged(); } }

    private int _muxReceiveBuffer = 4194304;
    public int MuxReceiveBuffer { get => _muxReceiveBuffer; set { _muxReceiveBuffer = value; OnPropertyChanged(); } }

    private int _muxProtocol = 2;
    public int MuxProtocol { get => _muxProtocol; set { _muxProtocol = value; OnPropertyChanged(); } }

    // ── WebSocket ────────────────────────────────────────────────────────────
    private bool _websocketEnabled;
    public bool WebsocketEnabled { get => _websocketEnabled; set { _websocketEnabled = value; OnPropertyChanged(); } }

    private string _websocketPath = "";
    public string WebsocketPath { get => _websocketPath; set { _websocketPath = value; OnPropertyChanged(); } }

    private string _websocketHost = "";
    public string WebsocketHost { get => _websocketHost; set { _websocketHost = value; OnPropertyChanged(); } }

    // ── Shadowsocks AEAD ─────────────────────────────────────────────────────
    private bool _shadowsocksEnabled;
    public bool ShadowsocksEnabled { get => _shadowsocksEnabled; set { _shadowsocksEnabled = value; OnPropertyChanged(); } }

    private string _shadowsocksMethod = "AES-128-GCM";
    public string ShadowsocksMethod { get => _shadowsocksMethod; set { _shadowsocksMethod = value; OnPropertyChanged(); } }

    private string _shadowsocksPassword = "";
    public string ShadowsocksPassword { get => _shadowsocksPassword; set { _shadowsocksPassword = value; OnPropertyChanged(); } }

    // ── Router ───────────────────────────────────────────────────────────────
    private bool _routerEnabled;
    public bool RouterEnabled { get => _routerEnabled; set { _routerEnabled = value; OnPropertyChanged(); } }

    private string _routerDefaultPolicy = "proxy";
    public string RouterDefaultPolicy { get => _routerDefaultPolicy; set { _routerDefaultPolicy = value; OnPropertyChanged(); } }

    private string _routerDomainStrategy = "as_is";
    public string RouterDomainStrategy { get => _routerDomainStrategy; set { _routerDomainStrategy = value; OnPropertyChanged(); } }

    private string _routerGeoip = "geoip.dat";
    public string RouterGeoip { get => _routerGeoip; set { _routerGeoip = value; OnPropertyChanged(); } }

    private string _routerGeosite = "geosite.dat";
    public string RouterGeosite { get => _routerGeosite; set { _routerGeosite = value; OnPropertyChanged(); } }

    private ObservableCollection<RouterRule> _routerRules = new();
    public ObservableCollection<RouterRule> RouterRules { get => _routerRules; set { _routerRules = value; OnPropertyChanged(); } }

    // ── Forward proxy ────────────────────────────────────────────────────────
    private bool _forwardProxyEnabled;
    public bool ForwardProxyEnabled { get => _forwardProxyEnabled; set { _forwardProxyEnabled = value; OnPropertyChanged(); } }

    private string _forwardProxyAddr = "";
    public string ForwardProxyAddr { get => _forwardProxyAddr; set { _forwardProxyAddr = value; OnPropertyChanged(); } }

    private int _forwardProxyPort;
    public int ForwardProxyPort { get => _forwardProxyPort; set { _forwardProxyPort = value; OnPropertyChanged(); } }

    private string _forwardProxyUsername = "";
    public string ForwardProxyUsername { get => _forwardProxyUsername; set { _forwardProxyUsername = value; OnPropertyChanged(); } }

    private string _forwardProxyPassword = "";
    public string ForwardProxyPassword { get => _forwardProxyPassword; set { _forwardProxyPassword = value; OnPropertyChanged(); } }

    // Trojan log level (0=ALL,1=INFO,2=WARN,3=ERROR,4=FATAL,5=OFF)
    private int _trojanLogLevel = 1;
    public int TrojanLogLevel { get => _trojanLogLevel; set { _trojanLogLevel = value; OnPropertyChanged(); } }

    private int _ping = -1;

    [JsonIgnore]
    public int Ping
    {
        get => _ping;
        set { _ping = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public ServerConfig Clone()
    {
        var clone = new ServerConfig
        {
            Id = Guid.NewGuid().ToString(),
            Name = Name + " (copy)",
            Region = Region,
            RemoteAddr = RemoteAddr,
            RemotePort = RemotePort,
            Password = Password,
            VerifyCert = VerifyCert,
            Sni = Sni,
            Alpn = Alpn,
            Cert = Cert,
            Key = Key,
            Curves = Curves,
            Fingerprint = Fingerprint,
            Ech = Ech,
            EchConfig = EchConfig,
            NoDelay = NoDelay,
            KeepAlive = KeepAlive,
            PreferIpv4 = PreferIpv4,
            MuxEnabled = MuxEnabled,
            MuxConcurrency = MuxConcurrency,
            MuxIdleTimeout = MuxIdleTimeout,
            MuxStreamBuffer = MuxStreamBuffer,
            MuxReceiveBuffer = MuxReceiveBuffer,
            MuxProtocol = MuxProtocol,
            WebsocketEnabled = WebsocketEnabled,
            WebsocketPath = WebsocketPath,
            WebsocketHost = WebsocketHost,
            ShadowsocksEnabled = ShadowsocksEnabled,
            ShadowsocksMethod = ShadowsocksMethod,
            ShadowsocksPassword = ShadowsocksPassword,
            RouterEnabled = RouterEnabled,
            RouterDefaultPolicy = RouterDefaultPolicy,
            RouterDomainStrategy = RouterDomainStrategy,
            RouterGeoip = RouterGeoip,
            RouterGeosite = RouterGeosite,
            ForwardProxyEnabled = ForwardProxyEnabled,
            ForwardProxyAddr = ForwardProxyAddr,
            ForwardProxyPort = ForwardProxyPort,
            ForwardProxyUsername = ForwardProxyUsername,
            ForwardProxyPassword = ForwardProxyPassword,
            TrojanLogLevel = TrojanLogLevel
        };
        // Deep-copy router rules so mutating the clone's list/items does not affect the original
        clone.RouterRules = new ObservableCollection<RouterRule>();
        foreach (var r in RouterRules)
            clone.RouterRules.Add(r.Clone());
        return clone;
    }
}
