using Bunit;
using Karamel.Web.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Protected;
using System.Net;
using Xunit;

namespace Karamel.Web.Tests;

public class BackendWarmupBannerTests : BunitContext
{
    [Fact]
    public void BackendWarmupBanner_WhenHealthCheckSucceedsImmediately_ShouldHideBanner()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        Services.AddSingleton(new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://example.test")
        });

        var cut = Render<BackendWarmupBanner>();

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("Waking up the server… this can take up to a minute.", cut.Markup);
        });
    }

    [Fact]
    public void BackendWarmupBanner_WhenServerStartsAfterDelay_ShouldShowBannerThenHideIt()
    {
        var callCount = 0;
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage _, CancellationToken cancellationToken) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    await Task.Delay(2000, cancellationToken);
                    return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                }

                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        Services.AddSingleton(new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://example.test")
        });

        var cut = Render<BackendWarmupBanner>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Waking up the server… this can take up to a minute.", cut.Markup);
        }, TimeSpan.FromSeconds(15));

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("Waking up the server… this can take up to a minute.", cut.Markup);
        }, TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void BackendWarmupBanner_WhenHealthCheckTimesOut_ShouldShowRetryButton()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timed out"));

        Services.AddSingleton(new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://example.test")
        });

        var cut = Render<BackendWarmupBanner>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("The server is not reachable right now.", cut.Markup);
            Assert.Contains("Retry", cut.Markup);
        });
    }

    [Fact]
    public async Task BackendWarmupBanner_Dispose_ShouldCancelBackgroundLoopWithoutThrowing()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage _, CancellationToken cancellationToken) =>
            {
                await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        Services.AddSingleton(new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://example.test")
        });

        var cut = Render<BackendWarmupBanner>();

        await cut.Instance.DisposeAsync();

        Assert.NotNull(cut);
    }
}
