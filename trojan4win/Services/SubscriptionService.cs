using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using trojan4win.Models;

namespace trojan4win.Services;

public sealed class SubscriptionService : IDisposable
{
    private const int MaxRedirects = 10;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;

    public SubscriptionService(HttpClient? httpClient = null)
    {
        _ownsClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false
        });
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task UpdateAsync(SubscriptionConfig subscription, CancellationToken cancellationToken = default)
    {
        var snapshot = await FetchAsync(subscription.Url, subscription.AllowInsecureConnection,
            subscription.UserAgent, cancellationToken);
        var previous = subscription.Servers.ToDictionary(x => x.SourceKey, StringComparer.Ordinal);
        foreach (var item in snapshot.Servers)
        {
            if (previous.TryGetValue(item.SourceKey, out var old))
                item.Server.Id = old.Server.Id;
        }

        subscription.Servers = snapshot.Servers;
        subscription.Headers = snapshot.Headers;
        subscription.ProfileTitle = snapshot.ProfileTitle;
        subscription.ProfileWebPageUrl = snapshot.ProfileWebPageUrl;
        subscription.Announce = snapshot.Announce;
        subscription.Upload = snapshot.Upload;
        subscription.Download = snapshot.Download;
        subscription.Total = snapshot.Total;
        subscription.Expire = snapshot.Expire;
        subscription.UpdateIntervalHours = snapshot.UpdateIntervalHours;
        subscription.LastUpdatedUtc = DateTimeOffset.UtcNow;
    }

    public async Task<SubscriptionSnapshot> FetchAsync(
        string url,
        bool allowInsecureConnection,
        string userAgent,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var current))
            throw new FormatException("A valid subscription URL is required.");

        HttpResponseMessage? response = null;
        for (var redirects = 0; redirects <= MaxRedirects; redirects++)
        {
            EnsureAllowed(current, allowInsecureConnection);
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            if (!string.IsNullOrWhiteSpace(userAgent))
                request.Headers.UserAgent.ParseAdd(userAgent);
            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!IsRedirect(response.StatusCode))
                break;
            if (redirects == MaxRedirects || response.Headers.Location is null)
            {
                response.Dispose();
                throw new HttpRequestException("The subscription redirect is invalid.");
            }
            current = response.Headers.Location.IsAbsoluteUri
                ? response.Headers.Location
                : new Uri(current, response.Headers.Location);
            response.Dispose();
            response = null;
        }

        using (response)
        {
            if (response is null)
                throw new HttpRequestException("The subscription request failed.");
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var lines = body.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length == 0)
                throw new FormatException("The subscription contains no servers.");

            var servers = new List<SubscriptionServer>(lines.Length);
            foreach (var line in lines)
            {
                servers.Add(new SubscriptionServer
                {
                    SourceKey = line,
                    Server = TrojanUriParser.Parse(line)
                });
            }

            var headers = ReadHeaders(response);
            var interval = ReadPositiveInt(headers, "Profile-Update-Interval", 1);
            var (upload, download, total, expire) = ParseUserInfo(
                headers.GetValueOrDefault("Subscription-Userinfo"));
            return new SubscriptionSnapshot(
                servers,
                headers,
                DecodeBase64Header(headers.GetValueOrDefault("Profile-Title")),
                EmptyToNull(headers.GetValueOrDefault("Profile-Web-Page-Url")),
                DecodeBase64Header(headers.GetValueOrDefault("Announce")),
                upload,
                download,
                total,
                expire,
                interval);
        }
    }

    private static Dictionary<string, string> ReadHeaders(HttpResponseMessage response)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in response.Headers)
            result[header.Key] = string.Join(",", header.Value);
        foreach (var header in response.Content.Headers)
            result[header.Key] = string.Join(",", header.Value);
        return result;
    }

    private static (long Upload, long Download, long Total, long Expire) ParseUserInfo(string? value)
    {
        long upload = 0, download = 0, total = 0, expire = 0;
        foreach (var part in (value ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Trim().Split('=', 2);
            if (pair.Length != 2 || !long.TryParse(pair[1], out var parsed)) continue;
            switch (pair[0].ToLowerInvariant())
            {
                case "upload": upload = parsed; break;
                case "download": download = parsed; break;
                case "total": total = parsed; break;
                case "expire": expire = parsed; break;
            }
        }
        return (upload, download, total, expire);
    }

    private static string? DecodeBase64Header(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!value.StartsWith("base64:", StringComparison.OrdinalIgnoreCase)) return value;
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(value[7..])); }
        catch (FormatException) { return null; }
    }

    private static int ReadPositiveInt(Dictionary<string, string> headers, string name, int fallback) =>
        headers.TryGetValue(name, out var value) && int.TryParse(value, out var parsed) && parsed > 0
            ? parsed
            : fallback;

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static bool IsRedirect(HttpStatusCode status) =>
        status is HttpStatusCode.Moved or HttpStatusCode.Redirect or HttpStatusCode.RedirectMethod
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    private static void EnsureAllowed(Uri uri, bool allowInsecure)
    {
        if (uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return;
        if (allowInsecure && uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)) return;
        throw new InvalidOperationException("Only HTTPS subscriptions are allowed unless insecure HTTP is enabled.");
    }

    public void Dispose()
    {
        if (_ownsClient) _httpClient.Dispose();
    }
}

public sealed record SubscriptionSnapshot(
    List<SubscriptionServer> Servers,
    Dictionary<string, string> Headers,
    string? ProfileTitle,
    string? ProfileWebPageUrl,
    string? Announce,
    long Upload,
    long Download,
    long Total,
    long Expire,
    int UpdateIntervalHours);
