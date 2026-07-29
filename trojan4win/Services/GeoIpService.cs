using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using trojan4win.Models;

namespace trojan4win.Services;

public static class GeoIpService
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, CacheEntry> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static async Task PopulateRegionAsync(ServerConfig server)
    {
        if (!string.IsNullOrWhiteSpace(server.Region) || string.IsNullOrWhiteSpace(server.RouterGeoip))
            return;
        try
        {
            var addresses = IPAddress.TryParse(server.RemoteAddr, out var parsed)
                ? new[] { parsed }
                : await Dns.GetHostAddressesAsync(server.RemoteAddr);
            var address = addresses.FirstOrDefault(IsPublic);
            if (address is null) return;
            var country = Lookup(ResolvePath(server.RouterGeoip), address);
            if (country is { Length: 2 })
                server.Region = country.ToUpperInvariant();
        }
        catch
        {
            // GeoIP is best effort; never block saving or importing a server.
        }
    }

    public static string? Lookup(string path, IPAddress address)
    {
        var fullPath = Path.GetFullPath(path);
        var modified = File.GetLastWriteTimeUtc(fullPath);
        CacheEntry entry;
        lock (Sync)
        {
            if (!Cache.TryGetValue(fullPath, out entry!) || entry.ModifiedUtc != modified)
            {
                entry = new CacheEntry(modified, Parse(File.ReadAllBytes(fullPath)));
                Cache[fullPath] = entry;
            }
        }

        var bytes = address.MapToIPv6().GetAddressBytes();
        foreach (var range in entry.Ranges)
        {
            var candidate = range.Address.Length == 4
                ? address.MapToIPv4().GetAddressBytes()
                : bytes;
            if (candidate.Length == range.Address.Length && Matches(candidate, range.Address, range.Prefix))
                return range.CountryCode;
        }
        return null;
    }

    internal static string ResolvePath(string path, string? baseDirectory = null)
    {
        if (Path.IsPathFullyQualified(path))
            return Path.GetFullPath(path);

        baseDirectory ??= AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDirectory, path),
            Path.Combine(baseDirectory, "Tools", "trojan", path),
            Path.GetFullPath(path),
        };
        return candidates.FirstOrDefault(File.Exists) ?? candidates[1];
    }

    private static List<Range> Parse(byte[] data)
    {
        var result = new List<Range>();
        foreach (var listField in Fields(data).Where(x => x.Number == 1 && x.Wire == 2))
        {
            string? country = null;
            var cidrs = new List<(byte[] Address, int Prefix)>();
            foreach (var geoField in Fields(listField.Bytes!))
            {
                if (geoField.Number == 1 && geoField.Wire == 2)
                    country = System.Text.Encoding.UTF8.GetString(geoField.Bytes!);
                else if (geoField.Number == 2 && geoField.Wire == 2)
                {
                    byte[]? ip = null;
                    var prefix = -1;
                    foreach (var cidrField in Fields(geoField.Bytes!))
                    {
                        if (cidrField.Number == 1 && cidrField.Wire == 2) ip = cidrField.Bytes;
                        if (cidrField.Number == 2 && cidrField.Wire == 0) prefix = checked((int)cidrField.Varint);
                    }
                    if (ip is { Length: 4 or 16 } && prefix >= 0 && prefix <= ip.Length * 8)
                        cidrs.Add((ip, prefix));
                }
            }
            if (country is not { Length: 2 }) continue;
            result.AddRange(cidrs.Select(x => new Range(country.ToUpperInvariant(), x.Address, x.Prefix)));
        }
        return result.OrderByDescending(x => x.Prefix).ToList();
    }

    private static IEnumerable<Field> Fields(byte[] data)
    {
        var offset = 0;
        while (offset < data.Length)
        {
            var key = ReadVarint(data, ref offset);
            var number = checked((int)(key >> 3));
            var wire = (int)(key & 7);
            if (wire == 0)
            {
                yield return new Field(number, wire, null, ReadVarint(data, ref offset));
            }
            else if (wire == 2)
            {
                var length = checked((int)ReadVarint(data, ref offset));
                if (length < 0 || offset + length > data.Length) throw new InvalidDataException();
                var bytes = data.AsSpan(offset, length).ToArray();
                offset += length;
                yield return new Field(number, wire, bytes, 0);
            }
            else
            {
                throw new InvalidDataException($"Unsupported protobuf wire type {wire}.");
            }
        }
    }

    private static ulong ReadVarint(byte[] data, ref int offset)
    {
        ulong value = 0;
        for (var shift = 0; shift < 64; shift += 7)
        {
            if (offset >= data.Length) throw new InvalidDataException();
            var current = data[offset++];
            value |= (ulong)(current & 0x7f) << shift;
            if ((current & 0x80) == 0) return value;
        }
        throw new InvalidDataException();
    }

    private static bool Matches(byte[] address, byte[] network, int prefix)
    {
        var fullBytes = prefix / 8;
        if (!address.AsSpan(0, fullBytes).SequenceEqual(network.AsSpan(0, fullBytes))) return false;
        var remaining = prefix % 8;
        if (remaining == 0) return true;
        var mask = (byte)(0xff << (8 - remaining));
        return (address[fullBytes] & mask) == (network[fullBytes] & mask);
    }

    private static bool IsPublic(IPAddress address)
    {
        if (IPAddress.IsLoopback(address)) return false;
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return b[0] != 10 && b[0] != 127 &&
                !(b[0] == 169 && b[1] == 254) &&
                !(b[0] == 172 && b[1] is >= 16 and <= 31) &&
                !(b[0] == 192 && b[1] == 168);
        }
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return !address.IsIPv6LinkLocal && !address.IsIPv6SiteLocal &&
                !address.IsIPv6Multicast && !address.Equals(IPAddress.IPv6Loopback);
        return false;
    }

    private sealed record CacheEntry(DateTime ModifiedUtc, List<Range> Ranges);
    private sealed record Range(string CountryCode, byte[] Address, int Prefix);
    private sealed record Field(int Number, int Wire, byte[]? Bytes, ulong Varint);
}
