using System;
using System.Collections.Generic;
using System.Globalization;
using trojan4win.Models;

namespace trojan4win.Services;

public static class TrojanUriParser
{
    public static ServerConfig Parse(string value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) ||
            !uri.Scheme.Equals("trojan", StringComparison.OrdinalIgnoreCase))
            throw new FormatException("A valid trojan:// URI is required.");
        if (string.IsNullOrWhiteSpace(uri.UserInfo))
            throw new FormatException("The Trojan password is missing.");
        if (string.IsNullOrWhiteSpace(uri.Host) || uri.Port is < 1 or > 65535)
            throw new FormatException("The server address or port is invalid.");

        var server = new ServerConfig
        {
            Password = Uri.UnescapeDataString(uri.UserInfo),
            RemoteAddr = uri.Host.Trim('[', ']'),
            RemotePort = uri.Port,
            Name = Decode(uri.Fragment.TrimStart('#')),
            TrojanLogLevel = 5
        };
        if (string.IsNullOrWhiteSpace(server.Name))
            throw new FormatException("The server name is missing.");

        foreach (var (name, rawValue) in ParseQuery(uri.Query))
            Apply(server, name, rawValue);
        return server;
    }

    private static void Apply(ServerConfig server, string name, string value)
    {
        switch (name)
        {
            case "allowInsecure": server.VerifyCert = !Boolean(value, name); break;
            case "sni": server.Sni = value; break;
            case "alpn": server.Alpn = value; break;
            case "cert": server.Cert = value; break;
            case "key": server.Key = value; break;
            case "curves": server.Curves = value; break;
            case "fingerprint":
            case "fp": server.Fingerprint = value; break;
            case "ech": server.Ech = Boolean(value, name); break;
            case "echConfig": server.EchConfig = value; break;
            case "tfo": server.NoDelay = Boolean(value, name); break;
            case "keepAlive": server.KeepAlive = Boolean(value, name); break;
            case "preferIpv4": server.PreferIpv4 = Boolean(value, name); break;
            case "mux": server.MuxEnabled = Boolean(value, name); break;
            case "muxConcurrency": server.MuxConcurrency = Integer(value, name, 1); break;
            case "muxIdleTimeout": server.MuxIdleTimeout = Integer(value, name, 0); break;
            case "muxStreamBuffer": server.MuxStreamBuffer = Integer(value, name, 0); break;
            case "muxReceiveBuffer": server.MuxReceiveBuffer = Integer(value, name, 0); break;
            case "muxProtocol": server.MuxProtocol = Integer(value, name, 1, 2); break;
            case "type":
                if (value == "ws") server.WebsocketEnabled = true;
                else if (value is "tcp" or "none") server.WebsocketEnabled = false;
                else throw new FormatException($"Unsupported value for '{name}'.");
                break;
            case "path": server.WebsocketPath = value; break;
            case "host": server.WebsocketHost = value; break;
            case "shadowsocksEnabled": server.ShadowsocksEnabled = Boolean(value, name); break;
            case "shadowsocksMethod": server.ShadowsocksMethod = value; break;
            case "shadowsocksPassword": server.ShadowsocksPassword = value; break;
            case "forwardProxyEnabled": server.ForwardProxyEnabled = Boolean(value, name); break;
            case "forwardProxyAddr": server.ForwardProxyAddr = value; break;
            case "forwardProxyPort": server.ForwardProxyPort = Integer(value, name, 1, 65535); break;
            case "forwardProxyUsername": server.ForwardProxyUsername = value; break;
            case "forwardProxyPassword": server.ForwardProxyPassword = value; break;
            default:
                break;
        }
    }

    private static IEnumerable<(string Name, string Value)> ParseQuery(string query)
    {
        foreach (var item in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = item.IndexOf('=');
            var name = Decode(separator < 0 ? item : item[..separator]);
            var value = Decode(separator < 0 ? "" : item[(separator + 1)..]);
            yield return (name, value);
        }
    }

    private static bool Boolean(string value, string name) => value.ToLowerInvariant() switch
    {
        "1" or "true" => true,
        "0" or "false" => false,
        _ => throw new FormatException($"'{name}' must be a boolean.")
    };

    private static int Integer(string value, string name, int minimum, int maximum = int.MaxValue)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ||
            parsed < minimum || parsed > maximum)
            throw new FormatException($"'{name}' has an invalid integer value.");
        return parsed;
    }

    private static string Decode(string value) => Uri.UnescapeDataString(value);
}
