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
            "&path=%2Fsocket%3Fx%3D1%2Braw+plus&host=cdn.example&tfo=0&mux=true&muxConcurrency=4" +
            "&shadowsocksEnabled=1&shadowsocksMethod=AES-256-GCM" +
            "&forwardProxyEnabled=1&forwardProxyPort=8080&unknown=ignored#%D0%A2%D0%B5%D1%81%D1%82");

        Assert.Equal("login:p@ss", server.Password);
        Assert.Equal("2001:db8::1", server.RemoteAddr);
        Assert.Equal(8443, server.RemotePort);
        Assert.Equal("Тест", server.Name);
        Assert.False(server.VerifyCert);
        Assert.True(server.WebsocketEnabled);
        Assert.Equal("/socket?x=1+raw+plus", server.WebsocketPath);
        Assert.False(server.NoDelay);
        Assert.True(server.MuxEnabled);
        Assert.Equal(4, server.MuxConcurrency);
        Assert.Equal(5, server.TrojanLogLevel);
    }

    [Theory]
    [InlineData("trojan://secret@example.com:443?allowInsecure=maybe#Name")]
    [InlineData("trojan://secret@example.com:443?forwardProxyPort=text#Name")]
    [InlineData("trojan://secret@example.com:443?type=grpc#Name")]
    public void RejectsInvalidSupportedParameterTypes(string uri)
    {
        Assert.Throws<FormatException>(() => TrojanUriParser.Parse(uri));
    }
}
