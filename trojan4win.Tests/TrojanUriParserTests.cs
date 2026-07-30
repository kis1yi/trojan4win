using System;
using trojan4win.Services;
using Xunit;

namespace trojan4win.Tests;

public sealed class TrojanUriParserTests
{
    [Fact]
    public void ParsesEscapedCredentialsIpv6NameAndAllCoreOptions()
    {
        var server = TrojanUriParser.Parse(
            "trojan://login%3Ap%40ss@[2001:db8::1]:8443?allowInsecure=1&type=ws" +
            "&path=%2Fsocket%3Fx%3D1%2Braw+plus&host=cdn.example&echEnabled=1" +
            "&ech=ECH%2BBASE64%3D%3D&tcpNoDelay=0&mux=true&muxConcurrency=4" +
            "&shadowsocksEnabled=1&shadowsocksMethod=AES-256-GCM" +
            "&forwardProxyEnabled=1&forwardProxyPort=8080&unknown=ignored#%D0%A2%D0%B5%D1%81%D1%82");

        Assert.Equal("login:p@ss", server.Password);
        Assert.Equal("2001:db8::1", server.RemoteAddr);
        Assert.Equal(8443, server.RemotePort);
        Assert.Equal("Тест", server.Name);
        Assert.False(server.VerifyCert);
        Assert.True(server.WebsocketEnabled);
        Assert.Equal("/socket?x=1+raw+plus", server.WebsocketPath);
        Assert.True(server.Ech);
        Assert.Equal("ECH+BASE64==", server.EchConfig);
        Assert.False(server.NoDelay);
        Assert.True(server.MuxEnabled);
        Assert.Equal(4, server.MuxConcurrency);
        Assert.Equal(5, server.TrojanLogLevel);
    }

    [Fact]
    public void AcceptsOnlyTcpAndWsTransportTypes()
    {
        Assert.False(TrojanUriParser.Parse(
            "trojan://secret@example.com:443?type=tcp#TCP").WebsocketEnabled);
        Assert.True(TrojanUriParser.Parse(
            "trojan://secret@example.com:443?type=ws#WS").WebsocketEnabled);

        foreach (var type in new[] { "none", "grpc", "httpupgrade", "xhttp" })
        {
            Assert.Throws<FormatException>(() => TrojanUriParser.Parse(
                $"trojan://secret@example.com:443?type={type}#Name"));
        }
    }

    [Theory]
    [InlineData("trojan://secret@example.com:443?allowInsecure=maybe#Name")]
    [InlineData("trojan://secret@example.com:443?echEnabled=maybe#Name")]
    [InlineData("trojan://secret@example.com:443?tcpNoDelay=maybe#Name")]
    [InlineData("trojan://secret@example.com:443?forwardProxyPort=text#Name")]
    public void RejectsInvalidSupportedParameterValues(string uri)
    {
        Assert.Throws<FormatException>(() => TrojanUriParser.Parse(uri));
    }

    [Fact]
    public void LegacyEchAndTfoParameterNamesAreNotAliases()
    {
        var server = TrojanUriParser.Parse(
            "trojan://secret@example.com:443?echConfig=legacy&tfo=0#Name");

        Assert.False(server.Ech);
        Assert.Equal("", server.EchConfig);
        Assert.True(server.NoDelay);
    }
}
