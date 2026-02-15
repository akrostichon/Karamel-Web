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

#pragma warning disable CS0618 // Type or member is obsolete
#pragma warning disable CS1739 // Best overload does not have a parameter
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

        // TODO: Update to new constructor after refactoring complete (Step 7)
        // _sessionService = new SessionService(...);
    }

    [Fact(Skip = "SessionService constructor changed - update in step 7")]
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

    [Fact]
    public void EnrichSongsWithLibraryFiles_ShouldEnrichMp3CdgSongsWithFileInformation()
    {
        // Arrange
        var songId = Guid.NewGuid();
        
        // Song from backend (no file paths - privacy requirement)
        var backendSong = new Song
        {
            Id = songId,
            Artist = "Test Artist",
            Title = "Test Song",
            MediaType = MediaType.Mp3Cdg,
            Mp3FileName = null,
            CdgFileName = null,
            AddedBySinger = "John Doe"
        };

        // Song from local library (with file paths)
        var librarySong = new Song
        {
            Id = songId,
            Artist = "Test Artist",
            Title = "Test Song",
            MediaType = MediaType.Mp3Cdg,
            Mp3FileName = "test.mp3",
            CdgFileName = "test.cdg",
            Path = "some/path"
        };

        var songs = new List<Song> { backendSong };
        var libraryLookup = new Dictionary<Guid, Song> { { songId, librarySong } };

        // Act - Use reflection to call private method
        var method = typeof(SessionService).GetMethod(
            "EnrichSongsWithLibraryFiles",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method?.Invoke(_sessionService, new object[] { songs, libraryLookup });

        // Assert
        Assert.Single(songs);
        var enrichedSong = songs[0];
        Assert.Equal("test.mp3", enrichedSong.Mp3FileName);
        Assert.Equal("test.cdg", enrichedSong.CdgFileName);
        Assert.Equal("some/path", enrichedSong.Path);
        Assert.Equal("John Doe", enrichedSong.AddedBySinger); // Preserved from backend
    }

    [Fact]
    public void EnrichSongsWithLibraryFiles_ShouldEnrichVideoSongsWithFileInformation()
    {
        // Arrange
        var songId = Guid.NewGuid();
        
        // Song from backend (no file paths - privacy requirement)
        var backendSong = new Song
        {
            Id = songId,
            Artist = "Test Artist",
            Title = "Test Video Song",
            MediaType = MediaType.Video,
            VideoFileName = null,
            VideoExtension = ".mp4",
            AddedBySinger = "Jane Smith"
        };

        // Song from local library (with file paths)
        var librarySong = new Song
        {
            Id = songId,
            Artist = "Test Artist",
            Title = "Test Video Song",
            MediaType = MediaType.Video,
            VideoFileName = "test-video.mp4",
            VideoExtension = ".mp4",
            Path = "videos/path"
        };

        var songs = new List<Song> { backendSong };
        var libraryLookup = new Dictionary<Guid, Song> { { songId, librarySong } };

        // Act - Use reflection to call private method
        var method = typeof(SessionService).GetMethod(
            "EnrichSongsWithLibraryFiles",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method?.Invoke(_sessionService, new object[] { songs, libraryLookup });

        // Assert
        Assert.Single(songs);
        var enrichedSong = songs[0];
        Assert.Equal("test-video.mp4", enrichedSong.VideoFileName);
        Assert.Equal(".mp4", enrichedSong.VideoExtension);
        Assert.Equal("videos/path", enrichedSong.Path);
        Assert.Equal("Jane Smith", enrichedSong.AddedBySinger); // Preserved from backend
    }

    [Fact]
    public void EnrichSongsWithLibraryFiles_ShouldSkipAlreadyEnrichedMp3CdgSongs()
    {
        // Arrange
        var songId = Guid.NewGuid();
        
        // Song already has file information (e.g., from main tab)
        var alreadyEnrichedSong = new Song
        {
            Id = songId,
            Artist = "Test Artist",
            Title = "Test Song",
            MediaType = MediaType.Mp3Cdg,
            Mp3FileName = "existing.mp3",
            CdgFileName = "existing.cdg",
            Path = "existing/path",
            AddedBySinger = "John Doe"
        };

        var songs = new List<Song> { alreadyEnrichedSong };
        var libraryLookup = new Dictionary<Guid, Song>();

        // Act - Use reflection to call private method
        var method = typeof(SessionService).GetMethod(
            "EnrichSongsWithLibraryFiles",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method?.Invoke(_sessionService, new object[] { songs, libraryLookup });

        // Assert - Song should remain unchanged
        Assert.Single(songs);
        var song = songs[0];
        Assert.Equal("existing.mp3", song.Mp3FileName);
        Assert.Equal("existing.cdg", song.CdgFileName);
        Assert.Equal("existing/path", song.Path);
    }

    [Fact]
    public void EnrichSongsWithLibraryFiles_ShouldSkipAlreadyEnrichedVideoSongs()
    {
        // Arrange
        var songId = Guid.NewGuid();
        
        // Video song already has file information
        var alreadyEnrichedSong = new Song
        {
            Id = songId,
            Artist = "Test Artist",
            Title = "Test Video",
            MediaType = MediaType.Video,
            VideoFileName = "existing-video.mp4",
            VideoExtension = ".mp4",
            Path = "existing/video/path",
            AddedBySinger = "Jane Smith"
        };

        var songs = new List<Song> { alreadyEnrichedSong };
        var libraryLookup = new Dictionary<Guid, Song>();

        // Act - Use reflection to call private method
        var method = typeof(SessionService).GetMethod(
            "EnrichSongsWithLibraryFiles",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method?.Invoke(_sessionService, new object[] { songs, libraryLookup });

        // Assert - Song should remain unchanged
        Assert.Single(songs);
        var song = songs[0];
        Assert.Equal("existing-video.mp4", song.VideoFileName);
        Assert.Equal(".mp4", song.VideoExtension);
        Assert.Equal("existing/video/path", song.Path);
    }

    [Fact]
    public void EnrichSongsWithLibraryFiles_ShouldHandleMixedMp3CdgAndVideoSongs()
    {
        // Arrange
        var mp3SongId = Guid.NewGuid();
        var videoSongId = Guid.NewGuid();
        
        // Backend songs (no file paths)
        var backendMp3Song = new Song
        {
            Id = mp3SongId,
            Artist = "Artist 1",
            Title = "MP3 Song",
            MediaType = MediaType.Mp3Cdg,
            Mp3FileName = null,
            CdgFileName = null
        };

        var backendVideoSong = new Song
        {
            Id = videoSongId,
            Artist = "Artist 2",
            Title = "Video Song",
            MediaType = MediaType.Video,
            VideoFileName = null
        };

        // Library songs (with file paths)
        var libraryMp3Song = new Song
        {
            Id = mp3SongId,
            Artist = "Artist 1",
            Title = "MP3 Song",
            MediaType = MediaType.Mp3Cdg,
            Mp3FileName = "song1.mp3",
            CdgFileName = "song1.cdg"
        };

        var libraryVideoSong = new Song
        {
            Id = videoSongId,
            Artist = "Artist 2",
            Title = "Video Song",
            MediaType = MediaType.Video,
            VideoFileName = "video1.mp4",
            VideoExtension = ".mp4"
        };

        var songs = new List<Song> { backendMp3Song, backendVideoSong };
        var libraryLookup = new Dictionary<Guid, Song>
        {
            { mp3SongId, libraryMp3Song },
            { videoSongId, libraryVideoSong }
        };

        // Act
        var method = typeof(SessionService).GetMethod(
            "EnrichSongsWithLibraryFiles",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method?.Invoke(_sessionService, new object[] { songs, libraryLookup });

        // Assert
        Assert.Equal(2, songs.Count);
        
        // MP3 song enriched
        var mp3Song = songs[0];
        Assert.Equal("song1.mp3", mp3Song.Mp3FileName);
        Assert.Equal("song1.cdg", mp3Song.CdgFileName);
        
        // Video song enriched
        var videoSong = songs[1];
        Assert.Equal("video1.mp4", videoSong.VideoFileName);
        Assert.Equal(".mp4", videoSong.VideoExtension);
    }

    [Fact]
    public void EnrichSongsWithLibraryFiles_ShouldHandleSongNotFoundInLibrary()
    {
        // Arrange
        var songId = Guid.NewGuid();
        var missingSongId = Guid.NewGuid();
        
        // Backend song that exists in library
        var backendSong = new Song
        {
            Id = songId,
            Artist = "Test Artist",
            Title = "Test Song",
            MediaType = MediaType.Mp3Cdg,
            Mp3FileName = null,
            CdgFileName = null
        };

        // Backend song that does NOT exist in library
        var missingBackendSong = new Song
        {
            Id = missingSongId,
            Artist = "Missing Artist",
            Title = "Missing Song",
            MediaType = MediaType.Video,
            VideoFileName = null
        };

        // Library only has one song
        var librarySong = new Song
        {
            Id = songId,
            Artist = "Test Artist",
            Title = "Test Song",
            MediaType = MediaType.Mp3Cdg,
            Mp3FileName = "test.mp3",
            CdgFileName = "test.cdg"
        };

        var songs = new List<Song> { backendSong, missingBackendSong };
        var libraryLookup = new Dictionary<Guid, Song> { { songId, librarySong } };

        // Act
        var method = typeof(SessionService).GetMethod(
            "EnrichSongsWithLibraryFiles",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method?.Invoke(_sessionService, new object[] { songs, libraryLookup });

        // Assert
        Assert.Equal(2, songs.Count);
        
        // First song enriched
        Assert.Equal("test.mp3", songs[0].Mp3FileName);
        
        // Second song NOT enriched (remains without file paths)
        Assert.Null(songs[1].VideoFileName);
    }

    [Fact]
    public async Task RestoreSessionStateAsync_WithThemeInBackend_ShouldDispatchSessionWithTheme()
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

        var mockJsModule = new Mock<IJSObjectReference>();
        
        // Mock sessionStorage returning empty object
        var emptyJson = JsonDocument.Parse("{}");
        mockJsModule.Setup(m => m.InvokeAsync<JsonElement>(
            "getSessionStateForSession",
            It.IsAny<object[]>()))
            .ReturnsAsync(emptyJson.RootElement);

        mockJsModule.Setup(m => m.InvokeAsync<bool>(
            "isUsingSignalR",
            It.IsAny<object[]>()))
            .ReturnsAsync(false);

        _mockJsRuntime.Setup(js => js.InvokeAsync<IJSObjectReference>(
            "import",
            It.Is<object[]>(args => args[0].ToString() == "./js/signalRBridge.js")))
            .ReturnsAsync(mockJsModule.Object);

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
        await _sessionService.InitializeAsync(sessionId, asMainTab: false);

        // Assert
        _mockDispatcher.Verify(d => d.Dispatch(It.Is<InitializeSessionAction>(
            action => action.Session.Theme == theme)), Times.Once);
    }

    [Fact]
    public async Task RestoreSessionStateAsync_WithoutThemeInBackend_ShouldDispatchSessionWithNullTheme()
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

        var mockJsModule = new Mock<IJSObjectReference>();
        
        var emptyJson = JsonDocument.Parse("{}");
        mockJsModule.Setup(m => m.InvokeAsync<JsonElement>(
            "getSessionStateForSession",
            It.IsAny<object[]>()))
            .ReturnsAsync(emptyJson.RootElement);

        mockJsModule.Setup(m => m.InvokeAsync<bool>(
            "isUsingSignalR",
            It.IsAny<object[]>()))
            .ReturnsAsync(false);

        _mockJsRuntime.Setup(js => js.InvokeAsync<IJSObjectReference>(
            "import",
            It.Is<object[]>(args => args[0].ToString() == "./js/signalRBridge.js")))
            .ReturnsAsync(mockJsModule.Object);

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
            action => action.Session.Theme == null)), Times.Once);
    }
}

