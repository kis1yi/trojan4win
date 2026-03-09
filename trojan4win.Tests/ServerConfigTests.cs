using System;
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
            Cipher = "AES128-SHA",
            CipherTls13 = "TLS_AES_128_GCM_SHA256",
            Cert = "/path/cert.pem",
            Key = "/path/key.pem",
            ReuseSession = false,
            SessionTicket = true,
            Curves = "P-256",
            NoDelay = false,
            KeepAlive = false,
            ReusePort = true,
            FastOpen = true,
            FastOpenQlen = 50,
            TrojanLogLevel = 3
        };

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
        Assert.Equal(original.Cipher, clone.Cipher);
        Assert.Equal(original.CipherTls13, clone.CipherTls13);
        Assert.Equal(original.Cert, clone.Cert);
        Assert.Equal(original.Key, clone.Key);
        Assert.Equal(original.ReuseSession, clone.ReuseSession);
        Assert.Equal(original.SessionTicket, clone.SessionTicket);
        Assert.Equal(original.Curves, clone.Curves);
        Assert.Equal(original.NoDelay, clone.NoDelay);
        Assert.Equal(original.KeepAlive, clone.KeepAlive);
        Assert.Equal(original.ReusePort, clone.ReusePort);
        Assert.Equal(original.FastOpen, clone.FastOpen);
        Assert.Equal(original.FastOpenQlen, clone.FastOpenQlen);
        Assert.Equal(original.TrojanLogLevel, clone.TrojanLogLevel);
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
        Assert.True(config.ReuseSession);
        Assert.False(config.SessionTicket);
        Assert.Equal("", config.Curves);
        Assert.Equal("", config.Cert);
        Assert.Equal("", config.Key);
        Assert.True(config.NoDelay);
        Assert.True(config.KeepAlive);
        Assert.False(config.ReusePort);
        Assert.False(config.FastOpen);
        Assert.Equal(20, config.FastOpenQlen);
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
}
