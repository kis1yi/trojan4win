using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace trojan4win.Models;

public class ServerConfig : INotifyPropertyChanged
{
    // CR-13: all persistent properties now raise OnPropertyChanged() so UI bindings
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

    private bool _verifyCert = true;
    public bool VerifyCert { get => _verifyCert; set { _verifyCert = value; OnPropertyChanged(); } }

    private string _sni = "";
    public string Sni { get => _sni; set { _sni = value; OnPropertyChanged(); } }

    private string _alpn = "h2,http/1.1";
    public string Alpn { get => _alpn; set { _alpn = value; OnPropertyChanged(); } }

    // SSL settings
    private string _cipher = "ECDHE-ECDSA-AES128-GCM-SHA256:ECDHE-RSA-AES128-GCM-SHA256:ECDHE-ECDSA-CHACHA20-POLY1305:ECDHE-RSA-CHACHA20-POLY1305:ECDHE-ECDSA-AES256-GCM-SHA384:ECDHE-RSA-AES256-GCM-SHA384:ECDHE-ECDSA-AES256-SHA:ECDHE-ECDSA-AES128-SHA:ECDHE-RSA-AES128-SHA:ECDHE-RSA-AES256-SHA:DHE-RSA-AES128-SHA:DHE-RSA-AES256-SHA:AES128-SHA:AES256-SHA:DES-CBC3-SHA";
    public string Cipher { get => _cipher; set { _cipher = value; OnPropertyChanged(); } }

    private string _cipherTls13 = "TLS_AES_128_GCM_SHA256:TLS_CHACHA20_POLY1305_SHA256:TLS_AES_256_GCM_SHA384";
    public string CipherTls13 { get => _cipherTls13; set { _cipherTls13 = value; OnPropertyChanged(); } }

    private string _cert = "";
    public string Cert { get => _cert; set { _cert = value; OnPropertyChanged(); } }

    private string _key = "";
    public string Key { get => _key; set { _key = value; OnPropertyChanged(); } }

    private bool _reuseSession = true;
    public bool ReuseSession { get => _reuseSession; set { _reuseSession = value; OnPropertyChanged(); } }

    private bool _sessionTicket;
    public bool SessionTicket { get => _sessionTicket; set { _sessionTicket = value; OnPropertyChanged(); } }

    private string _curves = "";
    public string Curves { get => _curves; set { _curves = value; OnPropertyChanged(); } }

    // TCP settings
    private bool _noDelay = true;
    public bool NoDelay { get => _noDelay; set { _noDelay = value; OnPropertyChanged(); } }

    private bool _keepAlive = true;
    public bool KeepAlive { get => _keepAlive; set { _keepAlive = value; OnPropertyChanged(); } }

    private bool _reusePort;
    public bool ReusePort { get => _reusePort; set { _reusePort = value; OnPropertyChanged(); } }

    private bool _fastOpen;
    public bool FastOpen { get => _fastOpen; set { _fastOpen = value; OnPropertyChanged(); } }

    private int _fastOpenQlen = 20;
    public int FastOpenQlen { get => _fastOpenQlen; set { _fastOpenQlen = value; OnPropertyChanged(); } }

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
        return new ServerConfig
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
            Cipher = Cipher,
            CipherTls13 = CipherTls13,
            Cert = Cert,
            Key = Key,
            ReuseSession = ReuseSession,
            SessionTicket = SessionTicket,
            Curves = Curves,
            NoDelay = NoDelay,
            KeepAlive = KeepAlive,
            ReusePort = ReusePort,
            FastOpen = FastOpen,
            FastOpenQlen = FastOpenQlen,
            TrojanLogLevel = TrojanLogLevel
        };
    }
}
