using Karamel.Web.Models;
using Karamel.Web.Services;
using Moq;
using System.Text.Json;

namespace Karamel.Web.Tests.TestHelpers;

/// <summary>
/// Fluent builder for creating and configuring ISessionService mocks with common test patterns
/// </summary>
public class SessionServiceMockBuilder
{
    private readonly Mock<ISessionService> _mock;
    private bool _isMainTab = true;
    private Guid _sessionId = Guid.NewGuid();

    public SessionServiceMockBuilder()
    {
        _mock = new Mock<ISessionService>();
        ConfigureDefaultBehavior();
    }

    /// <summary>
    /// Configure this mock as a main tab (default: true)
    /// </summary>
    public SessionServiceMockBuilder AsMainTab(bool isMainTab = true)
    {
        _isMainTab = isMainTab;
        ConfigureInitialize();
        ConfigureBroadcasts();
        return this;
    }

    /// <summary>
    /// Set the session ID for this mock
    /// </summary>
    public SessionServiceMockBuilder WithSessionId(Guid sessionId)
    {
        _sessionId = sessionId;
        ConfigureInitialize();
        return this;
    }

    /// <summary>
    /// Configure InitializeAsync to track calls and complete successfully
    /// </summary>
    public SessionServiceMockBuilder WithInitialize(Func<Guid, bool, Task>? callback = null)
    {
        _mock.Setup(s => s.InitializeAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
            .Returns((Guid id, bool asMain) => callback != null ? callback(id, asMain) : Task.CompletedTask);
        return this;
    }

    /// <summary>
    /// Configure broadcast methods with custom behavior
    /// </summary>
    public SessionServiceMockBuilder WithBroadcastPlaylist(Func<Task>? callback = null)
    {
        _mock.Setup(s => s.BroadcastPlaylistUpdatedAsync())
            .Returns(() => callback != null ? callback() : Task.CompletedTask);
        return this;
    }

    /// <summary>
    /// Configure broadcast session settings with custom behavior
    /// </summary>
    public SessionServiceMockBuilder WithBroadcastSettings(Func<Session, Task>? callback = null)
    {
        _mock.Setup(s => s.BroadcastSessionSettingsAsync(It.IsAny<Session>()))
            .Returns((Session session) => callback != null ? callback(session) : Task.CompletedTask);
        return this;
    }

    /// <summary>
    /// Configure broadcast current song with custom behavior
    /// </summary>
    public SessionServiceMockBuilder WithBroadcastCurrentSong(Func<Song?, string?, Task>? callback = null)
    {
        _mock.Setup(s => s.BroadcastCurrentSongAsync(It.IsAny<Song?>(), It.IsAny<string?>()))
            .Returns((Song? song, string? singer) => callback != null ? callback(song, singer) : Task.CompletedTask);
        return this;
    }

    /// <summary>
    /// Configure session URL generation
    /// </summary>
    public SessionServiceMockBuilder WithGenerateUrl(string baseUrl = "http://localhost:5000")
    {
        _mock.Setup(s => s.GenerateSessionUrlAsync(It.IsAny<string>(), It.IsAny<Guid>()))
            .Returns((string path, Guid id) => Task.FromResult($"{baseUrl}{path}?sessionId={id}"));
        return this;
    }

    /// <summary>
    /// Configure getting session ID from URL
    /// </summary>
    public SessionServiceMockBuilder WithGetSessionIdFromUrl(Guid? returnSessionId = null)
    {
        var sessionId = returnSessionId ?? _sessionId;
        _mock.Setup(s => s.GetSessionIdFromUrlAsync())
            .ReturnsAsync(sessionId);
        return this;
    }

    /// <summary>
    /// Configure OnStateUpdated callback behavior
    /// </summary>
    public SessionServiceMockBuilder WithOnStateUpdated(Action<string, JsonElement>? callback = null)
    {
        _mock.Setup(s => s.OnStateUpdated(It.IsAny<string>(), It.IsAny<JsonElement>()))
            .Callback((string type, JsonElement data) => callback?.Invoke(type, data));
        return this;
    }

    /// <summary>
    /// Build and return the configured mock
    /// </summary>
    public Mock<ISessionService> Build()
    {
        return _mock;
    }

    /// <summary>
    /// Build and return the mocked service object
    /// </summary>
    public ISessionService BuildObject()
    {
        return _mock.Object;
    }

    private void ConfigureDefaultBehavior()
    {
        // Default: all async methods complete successfully
        _mock.Setup(s => s.SaveLibraryToSessionStorageAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<Song>>()))
            .Returns(Task.CompletedTask);
        
        _mock.Setup(s => s.ClearSessionAsync())
            .Returns(Task.CompletedTask);
        
        _mock.Setup(s => s.CheckMainTabAliveAsync())
            .ReturnsAsync(true);
        
        _mock.Setup(s => s.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        ConfigureInitialize();
        ConfigureBroadcasts();
    }

    private void ConfigureInitialize()
    {
        _mock.Setup(s => s.InitializeAsync(It.IsAny<Guid>(), _isMainTab))
            .Returns(Task.CompletedTask);
    }

    private void ConfigureBroadcasts()
    {
        if (_isMainTab)
        {
            // Main tab: broadcasts complete successfully
            _mock.Setup(s => s.BroadcastPlaylistUpdatedAsync())
                .Returns(Task.CompletedTask);
            
            _mock.Setup(s => s.BroadcastSessionSettingsAsync(It.IsAny<Session>()))
                .Returns(Task.CompletedTask);
            
            _mock.Setup(s => s.BroadcastCurrentSongAsync(It.IsAny<Song?>(), It.IsAny<string?>()))
                .Returns(Task.CompletedTask);
        }
        else
        {
            // Secondary tab: broadcasts should not be called, but if they are, they complete silently
            _mock.Setup(s => s.BroadcastPlaylistUpdatedAsync())
                .Returns(Task.CompletedTask);
            
            _mock.Setup(s => s.BroadcastSessionSettingsAsync(It.IsAny<Session>()))
                .Returns(Task.CompletedTask);
            
            _mock.Setup(s => s.BroadcastCurrentSongAsync(It.IsAny<Song?>(), It.IsAny<string?>()))
                .Returns(Task.CompletedTask);
        }
    }
}
