using Bunit;
using Karamel.Web.Pages;
using Xunit;

namespace Karamel.Web.Tests;

public class PrivacyTests : IntegrationTestBase
{
    public PrivacyTests()
        : base(initialUrl: "https://karaoke.example.com/privacy")
    {
    }

    [Fact]
    public void Privacy_DescribesCurrentHostingAndTemporarySessionData()
    {
        var cut = Render<Privacy>();

        Assert.Contains("Cloudflare, Inc.", cut.Markup);
        Assert.Contains("Render Services, Inc.", cut.Markup);
        Assert.Contains("Frankfurt am Main", cut.Markup);
        Assert.Contains("spätestens 30 Minuten nach der letzten Aktivität", cut.Markup);
        Assert.Contains("Musikdateien verlassen das Gerät nicht", cut.Markup);
        Assert.Contains("Es werden keine Analyse- oder Tracking-Dienste eingesetzt.", cut.Markup);
        Assert.Contains("href=\"/impressum\"", cut.Markup);
        Assert.DoesNotContain("Application Insights", cut.Markup);
        Assert.DoesNotContain("Azure", cut.Markup);
    }
}
