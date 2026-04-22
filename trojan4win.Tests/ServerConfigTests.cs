using System;
using System.Collections.ObjectModel;
using trojan4win.Models;
using Xunit;

namespace trojan4win.Tests;

public class ServerConfigTests
{
    [Fact]
    public void Clone_GeneratesNewId_AndCopiesAllFields()
    {
        var original = new ServerConfig
        {
            Name = "My Server",
            Region = "EU",
            RemoteAddr = "example.com",
            RemotePort = 8443,
            Password = "secret",
            VerifyCert = false,
            Sni = "custom.sni",
            Alpn = "h2",
            Cert = "/path/cert.pem",
            Key = "/path/key.pem",
            Curves = "P-256",
            Fingerprint = "firefox",
            Ech = true,
            EchConfig = "AEj+DQBEb...",
            NoDelay = false,
            KeepAlive = false,
            PreferIpv4 = true,
            MuxEnabled = true,
            MuxConcurrency = 16,
            MuxIdleTimeout = 60,
            MuxStreamBuffer = 8388608,
            MuxReceiveBuffer = 8388608,
            MuxProtocol = 1,
            WebsocketEnabled = true,
            WebsocketPath = "/ws",
            WebsocketHost = "ws.example.com",
            ShadowsocksEnabled = true,
            ShadowsocksMethod = "AES-256-GCM",
            ShadowsocksPassword = "sspass",
            RouterEnabled = true,
            RouterDefaultPolicy = "bypass",
            RouterDomainStrategy = "ip_if_non_match",
            RouterGeoip = "custom.dat",
            RouterGeosite = "custom-site.dat",
            ForwardProxyEnabled = true,
            ForwardProxyAddr = "10.0.0.1",
            ForwardProxyPort = 3128,
            ForwardProxyUsername = "user",
            ForwardProxyPassword = "pw",
            TrojanLogLevel = 3
        };
        original.RouterRules.Add(new RouterRule { Policy = "bypass", Type = "cidr", Value = "10.0.0.0/8" });
        original.RouterRules.Add(new RouterRule { Policy = "proxy", Type = "domain", Value = "example.com" });

        var clone = original.Clone();

        Assert.NotEqual(original.Id, clone.Id);
        Assert.True(Guid.TryParse(clone.Id, out _), "Clone Id must be a valid GUID");
        Assert.Equal(original.Name + " (copy)", clone.Name);
        Assert.Equal(original.Region, clone.Region);
        Assert.Equal(original.RemoteAddr, clone.RemoteAddr);
        Assert.Equal(original.RemotePort, clone.RemotePort);
        Assert.Equal(original.Password, clone.Password);
        Assert.Equal(original.VerifyCert, clone.VerifyCert);
        Assert.Equal(original.Sni, clone.Sni);
        Assert.Equal(original.Alpn, clone.Alpn);
        Assert.Equal(original.Cert, clone.Cert);
        Assert.Equal(original.Key, clone.Key);
        Assert.Equal(original.Curves, clone.Curves);
        Assert.Equal(original.Fingerprint, clone.Fingerprint);
        Assert.Equal(original.Ech, clone.Ech);
        Assert.Equal(original.EchConfig, clone.EchConfig);
        Assert.Equal(original.NoDelay, clone.NoDelay);
        Assert.Equal(original.KeepAlive, clone.KeepAlive);
        Assert.Equal(original.PreferIpv4, clone.PreferIpv4);
        Assert.Equal(original.MuxEnabled, clone.MuxEnabled);
        Assert.Equal(original.MuxConcurrency, clone.MuxConcurrency);
        Assert.Equal(original.MuxIdleTimeout, clone.MuxIdleTimeout);
        Assert.Equal(original.MuxStreamBuffer, clone.MuxStreamBuffer);
        Assert.Equal(original.MuxReceiveBuffer, clone.MuxReceiveBuffer);
        Assert.Equal(original.MuxProtocol, clone.MuxProtocol);
        Assert.Equal(original.WebsocketEnabled, clone.WebsocketEnabled);
        Assert.Equal(original.WebsocketPath, clone.WebsocketPath);
        Assert.Equal(original.WebsocketHost, clone.WebsocketHost);
        Assert.Equal(original.ShadowsocksEnabled, clone.ShadowsocksEnabled);
        Assert.Equal(original.ShadowsocksMethod, clone.ShadowsocksMethod);
        Assert.Equal(original.ShadowsocksPassword, clone.ShadowsocksPassword);
        Assert.Equal(original.RouterEnabled, clone.RouterEnabled);
        Assert.Equal(original.RouterDefaultPolicy, clone.RouterDefaultPolicy);
        Assert.Equal(original.RouterDomainStrategy, clone.RouterDomainStrategy);
        Assert.Equal(original.RouterGeoip, clone.RouterGeoip);
        Assert.Equal(original.RouterGeosite, clone.RouterGeosite);
        Assert.Equal(original.ForwardProxyEnabled, clone.ForwardProxyEnabled);
        Assert.Equal(original.ForwardProxyAddr, clone.ForwardProxyAddr);
        Assert.Equal(original.ForwardProxyPort, clone.ForwardProxyPort);
        Assert.Equal(original.ForwardProxyUsername, clone.ForwardProxyUsername);
        Assert.Equal(original.ForwardProxyPassword, clone.ForwardProxyPassword);
        Assert.Equal(original.TrojanLogLevel, clone.TrojanLogLevel);

        // Router rule contents copied
        Assert.Equal(original.RouterRules.Count, clone.RouterRules.Count);
        for (var i = 0; i < original.RouterRules.Count; i++)
        {
            Assert.Equal(original.RouterRules[i].Policy, clone.RouterRules[i].Policy);
            Assert.Equal(original.RouterRules[i].Type, clone.RouterRules[i].Type);
            Assert.Equal(original.RouterRules[i].Value, clone.RouterRules[i].Value);
        }
    }

    [Fact]
    public void Clone_RouterRules_AreDeepCopied()
    {
        var original = new ServerConfig { RemoteAddr = "x.com", Password = "pw" };
        original.RouterRules.Add(new RouterRule { Policy = "proxy", Type = "domain", Value = "a.com" });
        original.RouterRules.Add(new RouterRule { Policy = "block", Type = "geoip", Value = "cn" });

        var clone = original.Clone();

        // Distinct collection references
        Assert.NotSame(original.RouterRules, clone.RouterRules);
        // Distinct rule instances
        Assert.NotSame(original.RouterRules[0], clone.RouterRules[0]);

        // Mutate clone: add, remove, and edit a rule's value
        clone.RouterRules.Add(new RouterRule { Policy = "bypass", Type = "cidr", Value = "192.168.0.0/16" });
        clone.RouterRules.RemoveAt(0);
        clone.RouterRules[0].Value = "mutated";

        // Original is untouched
        Assert.Equal(2, original.RouterRules.Count);
        Assert.Equal("proxy", original.RouterRules[0].Policy);
        Assert.Equal("a.com", original.RouterRules[0].Value);
        Assert.Equal("block", original.RouterRules[1].Policy);
        Assert.Equal("cn", original.RouterRules[1].Value);
    }

    [Fact]
    public void Clone_MultipleClones_AllHaveDistinctIds()
    {
        var original = new ServerConfig { RemoteAddr = "x.com", Password = "pw" };
        var clone1 = original.Clone();
        var clone2 = original.Clone();

        Assert.NotEqual(original.Id, clone1.Id);
        Assert.NotEqual(original.Id, clone2.Id);
        Assert.NotEqual(clone1.Id, clone2.Id);
    }

    [Fact]
    public void DefaultValues_MatchSpecification()
    {
        var config = new ServerConfig();

        Assert.False(string.IsNullOrEmpty(config.Id));
        Assert.True(Guid.TryParse(config.Id, out _), "Default Id must be a valid GUID");
        Assert.Equal("New Server", config.Name);
        Assert.Equal("", config.Region);
        Assert.Equal("", config.RemoteAddr);
        Assert.Equal(443, config.RemotePort);
        Assert.Equal("", config.Password);
        Assert.True(config.VerifyCert);
        Assert.Equal("", config.Sni);
        Assert.Equal("h2,http/1.1", config.Alpn);
        Assert.Equal("", config.Curves);
        Assert.Equal("", config.Cert);
        Assert.Equal("", config.Key);
        Assert.Equal("", config.Fingerprint);
        Assert.False(config.Ech);
        Assert.Equal("", config.EchConfig);
        Assert.True(config.NoDelay);
        Assert.True(config.KeepAlive);
        Assert.False(config.PreferIpv4);
        Assert.False(config.MuxEnabled);
        Assert.Equal(8, config.MuxConcurrency);
        Assert.Equal(30, config.MuxIdleTimeout);
        Assert.Equal(4194304, config.MuxStreamBuffer);
        Assert.Equal(4194304, config.MuxReceiveBuffer);
        Assert.Equal(2, config.MuxProtocol);
        Assert.False(config.WebsocketEnabled);
        Assert.Equal("", config.WebsocketPath);
        Assert.Equal("", config.WebsocketHost);
        Assert.False(config.ShadowsocksEnabled);
        Assert.Equal("AES-128-GCM", config.ShadowsocksMethod);
        Assert.Equal("", config.ShadowsocksPassword);
        Assert.False(config.RouterEnabled);
        Assert.Equal("proxy", config.RouterDefaultPolicy);
        Assert.Equal("as_is", config.RouterDomainStrategy);
        Assert.Equal("geoip.dat", config.RouterGeoip);
        Assert.Equal("geosite.dat", config.RouterGeosite);
        Assert.Empty(config.RouterRules);
        Assert.False(config.ForwardProxyEnabled);
        Assert.Equal("", config.ForwardProxyAddr);
        Assert.Equal(0, config.ForwardProxyPort);
        Assert.Equal("", config.ForwardProxyUsername);
        Assert.Equal("", config.ForwardProxyPassword);
        Assert.Equal(1, config.TrojanLogLevel);
        Assert.Equal(-1, config.Ping);
    }

    [Fact]
    public void PropertyChanged_IsFiredOnSet()
    {
        var config = new ServerConfig();
        string? changedProp = null;
        config.PropertyChanged += (_, e) => changedProp = e.PropertyName;

        config.Name = "Changed";

        Assert.Equal(nameof(config.Name), changedProp);
    }

    [Fact]
    public void RouterRule_PropertyChanged_IsFiredOnSet()
    {
        var rule = new RouterRule();
        string? changedProp = null;
        rule.PropertyChanged += (_, e) => changedProp = e.PropertyName;

        rule.Policy = "proxy";
        Assert.Equal(nameof(rule.Policy), changedProp);

        rule.Type = "domain";
        Assert.Equal(nameof(rule.Type), changedProp);

        rule.Value = "example.com";
        Assert.Equal(nameof(rule.Value), changedProp);
    }
}
