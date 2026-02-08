using Bunit;
using Fluxor;
using Karamel.Web.Models;
using Karamel.Web.Services;
using Karamel.Web.Store.Library;
using Karamel.Web.Store.Session;
using Microsoft.JSInterop;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Karamel.Web.Tests;

public class SessionServiceTests : TestContext
{
    private readonly Mock<IJSRuntime> _mockJsRuntime;
    private readonly Mock<IState<LibraryState>> _mockLibraryState;
    private readonly Mock<IDispatcher> _mockDispatcher;
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly HttpClient _httpClient;
    private SessionService _sessionService;

    public SessionServiceTests()
    {
        _mockJsRuntime = new Mock<IJSRuntime>();
        _mockLibraryState = new Mock<IState<LibraryState>>();
        _mockDispatcher = new Mock<IDispatcher>();
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        
        _httpClient = new HttpClient(_mockHttpHandler.Object)
        {
            BaseAddress = new Uri("https://api.test.com")
        };

        _sessionService = new SessionService(
            jsRuntime: _mockJsRuntime.Object,
            libraryState: _mockLibraryState.Object,
            dispatcher: _mockDispatcher.Object,
            httpClient: _httpClient
        );
    }

    [Fact]
    public async Task RestoreSessionStateAsync_WhenSessionStorageEmpty_ShouldFetchFromBackendAPI()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var sessionData = new
        {
            id = sessionId,
            requireSingerName = true,
            pauseBetweenSongsSeconds = 5,
            allowSingersToReorder = false
        };

        var mockJsModule = new Mock<IJSObjectReference>();
        
        // Mock sessionStorage returning empty object (no session data)
        var emptyJson = JsonDocument.Parse("{}");
        mockJsModule.Setup(m => m.InvokeAsync<JsonElement>(
            "getSessionStateForSession",
            It.IsAny<object[]>()))
            .ReturnsAsync(emptyJson.RootElement);

        // Mock isUsingSignalR
        mockJsModule.Setup(m => m.InvokeAsync<bool>(
            "isUsingSignalR",
            It.IsAny<object[]>()))
            .ReturnsAsync(false);

        _mockJsRuntime.Setup(js => js.InvokeAsync<IJSObjectReference>(
            "import",
            It.Is<object[]>(args => args[0].ToString() == "./js/signalRBridge.js")))
            .ReturnsAsync(mockJsModule.Object);

        // Mock HTTP response
        var responseJson = JsonSerializer.Serialize(sessionData);
        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        };

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri!.ToString().Contains($"/api/sessions/{sessionId}")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(responseMessage);

        // Act
        await _sessionService.InitializeAsync(sessionId, asMainTab: false);

        // Assert
        _mockDispatcher.Verify(d => d.Dispatch(It.Is<InitializeSessionAction>(
            action => action.Session.SessionId == sessionId &&
                     action.Session.RequireSingerName == true &&
                     action.Session.PauseBetweenSongsSeconds == 5)),
            Times.Once);
    }

    [Fact]
    public async Task RestoreSessionStateAsync_WhenBackendReturns404_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var mockJsModule = new Mock<IJSObjectReference>();
        
        // Mock sessionStorage returning empty object (no session data)
        var emptyJson = JsonDocument.Parse("{}");
        mockJsModule.Setup(m => m.InvokeAsync<JsonElement>(
            "getSessionStateForSession",
            It.IsAny<object[]>()))
            .ReturnsAsync(emptyJson.RootElement);

        // Mock isUsingSignalR
        mockJsModule.Setup(m => m.InvokeAsync<bool>(
            "isUsingSignalR",
            It.IsAny<object[]>()))
            .ReturnsAsync(false);

        _mockJsRuntime.Setup(js => js.InvokeAsync<IJSObjectReference>(
            "import",
            It.Is<object[]>(args => args[0].ToString() == "./js/signalRBridge.js")))
            .ReturnsAsync(mockJsModule.Object);

        // Mock HTTP 404 response
        var responseMessage = new HttpResponseMessage(HttpStatusCode.NotFound);

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(responseMessage);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sessionService.InitializeAsync(sessionId, asMainTab: false));

        Assert.Contains("expired or does not exist", exception.Message);
    }

    [Fact]
    public async Task RestoreSessionStateAsync_WhenNetworkError_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var mockJsModule = new Mock<IJSObjectReference>();
        
        // Mock sessionStorage returning empty object (no session data)
        var emptyJson = JsonDocument.Parse("{}");
        mockJsModule.Setup(m => m.InvokeAsync<JsonElement>(
            "getSessionStateForSession",
            It.IsAny<object[]>()))
            .ReturnsAsync(emptyJson.RootElement);

        // Mock isUsingSignalR
        mockJsModule.Setup(m => m.InvokeAsync<bool>(
            "isUsingSignalR",
            It.IsAny<object[]>()))
            .ReturnsAsync(false);

        _mockJsRuntime.Setup(js => js.InvokeAsync<IJSObjectReference>(
            "import",
            It.Is<object[]>(args => args[0].ToString() == "./js/signalRBridge.js")))
            .ReturnsAsync(mockJsModule.Object);

        // Mock HTTP network error
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sessionService.InitializeAsync(sessionId, asMainTab: false));

        Assert.Contains("Unable to connect to session", exception.Message);
    }

    [Fact]
    public async Task RestoreSessionStateAsync_WhenSessionStorageHasData_ShouldNotFetchFromBackend()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var sessionJson = JsonSerializer.Serialize(new
        {
            session = new
            {
                sessionId = sessionId.ToString(),
                requireSingerName = true,
                allowSingerReorder = false,
                pauseBetweenSongs = true,
                pauseBetweenSongsSeconds = 5,
                filenamePattern = "%artist - %title"
            }
        });

        var mockJsModule = new Mock<IJSObjectReference>();
        
        // Mock sessionStorage returning data
        var jsonDoc = JsonDocument.Parse(sessionJson);
        mockJsModule.Setup(m => m.InvokeAsync<JsonElement>(
            "getSessionStateForSession",
            It.IsAny<object[]>()))
            .ReturnsAsync(jsonDoc.RootElement);

        // Mock isUsingSignalR
        mockJsModule.Setup(m => m.InvokeAsync<bool>(
            "isUsingSignalR",
            It.IsAny<object[]>()))
            .ReturnsAsync(false);

        _mockJsRuntime.Setup(js => js.InvokeAsync<IJSObjectReference>(
            "import",
            It.Is<object[]>(args => args[0].ToString() == "./js/signalRBridge.js")))
            .ReturnsAsync(mockJsModule.Object);


        // Act
        await _sessionService.InitializeAsync(sessionId, asMainTab: false);

        // Assert - Should dispatch from sessionStorage, not HTTP
        _mockDispatcher.Verify(d => d.Dispatch(It.IsAny<InitializeSessionAction>()), Times.Once);
        
        // Verify HTTP was NOT called
        _mockHttpHandler.Protected()
            .Verify<Task<HttpResponseMessage>>(
                "SendAsync",
                Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
    }
}
