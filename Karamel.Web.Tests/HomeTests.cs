using System.Threading.Tasks;
using Bunit;
using Karamel.Web.Pages;
using Xunit;

namespace Karamel.Web.Tests
{
    public class HomeTests : IntegrationTestBase
    {
        [Fact]
        public async Task Home_ShowsBrowserNotSupported_WhenFileSystemApiUnavailable()
        {
            // IntegrationTestBase registers a mock IJSRuntime that returns default values for module calls.
            // The default for bool is false, which simulates File System Access API being unavailable.

            // Act
            var cut = RenderComponent<Home>();

            // Allow OnAfterRenderAsync to complete
            await Task.Delay(50);

            // Assert: banner is visible
            var alerts = cut.FindAll(".alert.alert-danger");
            Assert.NotEmpty(alerts);
            Assert.Contains("Browser Not Supported", alerts[0].TextContent);
        }
    }
}
