using Karamel.Web.Models;
using Karamel.Web.Services;
using Microsoft.JSInterop;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Karamel.Web.Tests;

/// <summary>
/// Tests for SessionApiClient - verifies backend API calls for session configuration
/// Tests error handling (404, network errors) and theme deserialization
/// </summary>
public class SessionApiClientTests
{
    private readonly Mock<IJSRuntime> _mockJsRuntime;
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly HttpClient _httpClient;
    private readonly SessionApiClient _service;

    public SessionApiClientTests()
    {
        _mockJsRuntime = new Mock<IJSRuntime>();
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        
        _httpClient = new HttpClient(_mockHttpHandler.Object)
        {
            BaseAddress = new Uri("https://api.test.com")
        };

        _service = new SessionApiClient(_mockJsRuntime.Object, _httpClient);
    }

    [Fact]
    public async Task FetchSessionConfigFromBackendAsync_WhenBackendReturns404_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Mock HTTP 404 response
        var responseMessage = new HttpResponseMessage(HttpStatusCode.NotFound);

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri!.ToString().Contains($"/api/sessions/{sessionId}")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(responseMessage);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.FetchSessionConfigFromBackendAsync(sessionId));

        Assert.Contains("expired or does not exist", exception.Message);
    }

    [Fact]
    public async Task FetchSessionConfigFromBackendAsync_WhenNetworkError_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Mock HTTP network error
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri!.ToString().Contains($"/api/sessions/{sessionId}")),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.FetchSessionConfigFromBackendAsync(sessionId));

        Assert.Contains("Unable to connect to session", exception.Message);
    }

    [Fact]
    public async Task FetchSessionConfigFromBackendAsync_WithThemeInBackend_ShouldReturnSessionWithTheme()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var theme = "dark";
        var sessionData = new
        {
            id = sessionId,
            requireSingerName = true,
            pauseBetweenSongsSeconds = 5,
            allowSingersToReorder = false,
            theme = theme
        };

        // Mock HTTP response with theme
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
        var result = await _service.FetchSessionConfigFromBackendAsync(sessionId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(sessionId, result.SessionId);
        Assert.Equal(theme, result.Theme);
        Assert.True(result.RequireSingerName);
        Assert.Equal(5, result.PauseBetweenSongsSeconds);
        Assert.False(result.AllowSingersToReorder);
    }

    [Fact]
    public async Task FetchSessionConfigFromBackendAsync_WithoutThemeInBackend_ShouldReturnSessionWithNullTheme()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var sessionData = new
        {
            id = sessionId,
            requireSingerName = true,
            pauseBetweenSongsSeconds = 5,
            allowSingersToReorder = false
            // No theme property
        };

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
        var result = await _service.FetchSessionConfigFromBackendAsync(sessionId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(sessionId, result.SessionId);
        Assert.Null(result.Theme);
        Assert.True(result.RequireSingerName);
        Assert.Equal(5, result.PauseBetweenSongsSeconds);
    }

    [Fact]
    public async Task FetchSessionConfigFromBackendAsync_WhenServerError500_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        var responseMessage = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("Internal server error", Encoding.UTF8, "text/plain")
        };

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri!.ToString().Contains($"/api/sessions/{sessionId}")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(responseMessage);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.FetchSessionConfigFromBackendAsync(sessionId));

        Assert.Contains("Failed to retrieve session configuration", exception.Message);
    }
}
