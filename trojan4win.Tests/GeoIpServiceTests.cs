using System;
using System.IO;
using System.Net;
using trojan4win.Services;
using Xunit;

namespace trojan4win.Tests;

public sealed class GeoIpServiceTests
{
    [Fact]
    public void ResolvesDefaultGeoIpRelativeToBundledTrojanDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"geoip-root-{Guid.NewGuid():N}");
        var tools = Path.Combine(root, "Tools", "trojan");
        Directory.CreateDirectory(tools);
        var expected = Path.Combine(tools, "geoip.dat");
        try
        {
            File.WriteAllBytes(expected, []);

            Assert.Equal(expected, GeoIpService.ResolvePath("geoip.dat", root));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ReadsOfficialGeoIpListWireFormatForIpv4AndIpv6()
    {
        var path = Path.Combine(Path.GetTempPath(), $"geoip-{Guid.NewGuid():N}.dat");
        try
        {
            File.WriteAllBytes(path, Message(
                1, Message(
                    1, Bytes("NL"),
                    2, Concat(
                        Message(1, IPAddress.Parse("203.0.113.0").GetAddressBytes()),
                        [0x10, 0x18])),
                1, Message(
                    1, Bytes("DE"),
                    2, Concat(
                        Message(1, IPAddress.Parse("2001:db8::").GetAddressBytes()),
                        [0x10, 0x20]))));

            Assert.Equal("NL", GeoIpService.Lookup(path, IPAddress.Parse("203.0.113.42")));
            Assert.Equal("DE", GeoIpService.Lookup(path, IPAddress.Parse("2001:db8::42")));
            Assert.Null(GeoIpService.Lookup(path, IPAddress.Parse("198.51.100.1")));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static byte[] Message(params object[] fields)
    {
        using var stream = new MemoryStream();
        for (var i = 0; i < fields.Length; i += 2)
        {
            var number = (int)fields[i];
            var value = (byte[])fields[i + 1];
            stream.WriteByte((byte)((number << 3) | 2));
            WriteVarint(stream, (ulong)value.Length);
            stream.Write(value);
        }
        return stream.ToArray();
    }

    private static byte[] Bytes(string value) => System.Text.Encoding.UTF8.GetBytes(value);

    private static byte[] Concat(byte[] first, byte[] second)
    {
        var result = new byte[first.Length + second.Length];
        first.CopyTo(result, 0);
        second.CopyTo(result, first.Length);
        return result;
    }

    private static void WriteVarint(Stream stream, ulong value)
    {
        while (value >= 0x80)
        {
            stream.WriteByte((byte)(value | 0x80));
            value >>= 7;
        }
        stream.WriteByte((byte)value);
    }
}
