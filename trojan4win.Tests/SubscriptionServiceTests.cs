using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using trojan4win.Models;
using trojan4win.Services;
using Xunit;

namespace trojan4win.Tests;

public sealed class SubscriptionServiceTests
{
    [Fact]
    public async Task ParsesBodyAndHappHeadersAndPreservesStableIds()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "trojan://secret@example.com:443#One\n",
                Encoding.UTF8,
                "text/plain")
        };
        response.Headers.TryAddWithoutValidation("profile-update-interval", "6");
        response.Headers.TryAddWithoutValidation(
            "Profile-Title",
            "base64:" + Convert.ToBase64String(Encoding.UTF8.GetBytes("Профиль")));
        response.Headers.TryAddWithoutValidation(
            "Subscription-Userinfo",
            "upload=10; download=20; total=-1; expire=2000000000");
        var handler = new StubHandler(response);
        using var service = new SubscriptionService(new HttpClient(handler));
        var subscription = new SubscriptionConfig
        {
            Url = "https://sub.example/sub",
            Servers =
            [
                new SubscriptionServer
                {
                    SourceKey = "trojan://secret@example.com:443#One",
                    Server = new ServerConfig { Id = "stable" }
                }
            ]
        };

        await service.UpdateAsync(subscription);

        Assert.Equal("Профиль", subscription.ProfileTitle);
        Assert.Equal(6, subscription.UpdateIntervalHours);
        Assert.Equal(-1, subscription.Total);
        Assert.Equal("stable", Assert.Single(subscription.Servers).Server.Id);
        Assert.NotNull(subscription.LastUpdatedUtc);
    }

    [Fact]
    public async Task InvalidLineLeavesExistingSnapshotUntouched()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "trojan://secret@example.com:443#One\nnot-a-uri",
                Encoding.UTF8,
                "text/plain")
        };
        using var service = new SubscriptionService(new HttpClient(new StubHandler(response)));
        var existing = new SubscriptionServer { SourceKey = "old", Server = new ServerConfig() };
        var subscription = new SubscriptionConfig
        {
            Url = "https://sub.example/sub",
            ProfileTitle = "Old",
            Servers = [existing]
        };

        await Assert.ThrowsAsync<FormatException>(() => service.UpdateAsync(subscription));

        Assert.Same(existing, Assert.Single(subscription.Servers));
        Assert.Equal("Old", subscription.ProfileTitle);
    }

    [Fact]
    public async Task HttpRequiresExplicitFlag()
    {
        using var service = new SubscriptionService(new HttpClient(new StubHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("trojan://secret@example.com:443#One")
            })));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.FetchAsync("http://sub.example/sub", false, "test"));
    }

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            response.RequestMessage = request;
            return Task.FromResult(response);
        }
    }
}
