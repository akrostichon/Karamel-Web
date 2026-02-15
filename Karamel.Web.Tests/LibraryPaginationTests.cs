using Karamel.Web.Models;
using Karamel.Web.Store.Library;
using Karamel.Web.Services;
using Fluxor;
using Moq;
using System.Text.Json;
using Xunit;

namespace Karamel.Web.Tests;

public class LibraryPaginationTests
{
    [Fact]
    public void ReduceLoadPageSuccess_AppendsWhenAppendTrue()
    {
        // Arrange: Initial state with 2 songs, page 1
        var song1 = new Song { Id = Guid.NewGuid(), Artist = "Artist 1", Title = "Title 1", Mp3FileName = "song1.mp3", CdgFileName = "song1.cdg" };
        var song2 = new Song { Id = Guid.NewGuid(), Artist = "Artist 2", Title = "Title 2", Mp3FileName = "song2.mp3", CdgFileName = "song2.cdg" };
        var initialState = new LibraryState
        {
            Songs = new List<Song> { song1, song2 },
            CurrentPage = 1,
            TotalCount = 100
        };

        // Act: Load page 2 with Append = true
        var song3 = new Song { Id = Guid.NewGuid(), Artist = "Artist 3", Title = "Title 3", Mp3FileName = "song3.mp3", CdgFileName = "song3.cdg" };
        var song4 = new Song { Id = Guid.NewGuid(), Artist = "Artist 4", Title = "Title 4", Mp3FileName = "song4.mp3", CdgFileName = "song4.cdg" };
        var action = new LoadPageSuccessAction(
            Songs: new List<Song> { song3, song4 },
            Page: 2,
            TotalCount: 100,
            SearchQuery: null,
            Append: true
        );
        var newState = LibraryReducers.ReduceLoadPageSuccess(initialState, action);

        // Assert: Songs are concatenated
        Assert.Equal(4, newState.Songs.Count);
        Assert.Equal(song1.Id, newState.Songs[0].Id);
        Assert.Equal(song2.Id, newState.Songs[1].Id);
        Assert.Equal(song3.Id, newState.Songs[2].Id);
        Assert.Equal(song4.Id, newState.Songs[3].Id);
        Assert.Equal(2, newState.CurrentPage);
        Assert.Equal(100, newState.TotalCount);
        Assert.Null(newState.ServerSearchQuery);
        Assert.False(newState.IsLoading);
    }

    [Fact]
    public void ReduceLoadPageSuccess_ReplacesWhenAppendFalse()
    {
        // Arrange: Initial state with 4 songs, page 2, search "Beatles"
        var song1 = new Song { Id = Guid.NewGuid(), Artist = "Beatles", Title = "Hey Jude", Mp3FileName = "hey.mp3", CdgFileName = "hey.cdg" };
        var song2 = new Song { Id = Guid.NewGuid(), Artist = "Beatles", Title = "Let It Be", Mp3FileName = "let.mp3", CdgFileName = "let.cdg" };
        var song3 = new Song { Id = Guid.NewGuid(), Artist = "Beatles", Title = "Yesterday", Mp3FileName = "yes.mp3", CdgFileName = "yes.cdg" };
        var song4 = new Song { Id = Guid.NewGuid(), Artist = "Beatles", Title = "Come Together", Mp3FileName = "come.mp3", CdgFileName = "come.cdg" };
        var initialState = new LibraryState
        {
            Songs = new List<Song> { song1, song2, song3, song4 },
            CurrentPage = 2,
            TotalCount = 150,
            ServerSearchQuery = "Beatles"
        };

        // Act: New search for "Stones" - page 1 with Append = false
        var newSong1 = new Song { Id = Guid.NewGuid(), Artist = "Rolling Stones", Title = "Paint It Black", Mp3FileName = "paint.mp3", CdgFileName = "paint.cdg" };
        var newSong2 = new Song { Id = Guid.NewGuid(), Artist = "Rolling Stones", Title = "Satisfaction", Mp3FileName = "sat.mp3", CdgFileName = "sat.cdg" };
        var newSong3 = new Song { Id = Guid.NewGuid(), Artist = "Rolling Stones", Title = "Sympathy", Mp3FileName = "sym.mp3", CdgFileName = "sym.cdg" };
        var action = new LoadPageSuccessAction(
            Songs: new List<Song> { newSong1, newSong2, newSong3 },
            Page: 1,
            TotalCount: 75,
            SearchQuery: "Stones",
            Append: false
        );
        var newState = LibraryReducers.ReduceLoadPageSuccess(initialState, action);

        // Assert: Songs are replaced
        Assert.Equal(3, newState.Songs.Count);
        Assert.Equal(newSong1.Id, newState.Songs[0].Id);
        Assert.Equal(newSong2.Id, newState.Songs[1].Id);
        Assert.Equal(newSong3.Id, newState.Songs[2].Id);
        Assert.Equal(1, newState.CurrentPage);
        Assert.Equal(75, newState.TotalCount);
        Assert.Equal("Stones", newState.ServerSearchQuery);
        Assert.False(newState.IsLoading);
    }

    [Fact]
    public void ReduceLoadPageSuccess_UpdatesTotalCount()
    {
        // Arrange
        var initialState = new LibraryState
        {
            Songs = new List<Song>(),
            CurrentPage = 1,
            PageSize = 50,
            TotalCount = 0
        };

        // Act: Load page with TotalCount = 150
        var song1 = new Song { Id = Guid.NewGuid(), Artist = "Test", Title = "Song 1", Mp3FileName = "test.mp3", CdgFileName = "test.cdg" };
        var action = new LoadPageSuccessAction(
            Songs: new List<Song> { song1 },
            Page: 1,
            TotalCount: 150,
            SearchQuery: null,
            Append: false
        );
        var newState = LibraryReducers.ReduceLoadPageSuccess(initialState, action);

        // Assert: TotalCount updated and HasMorePages calculated correctly
        Assert.Equal(150, newState.TotalCount);
        Assert.True(newState.HasMorePages); // 1 * 50 = 50 < 150
    }

    [Fact]
    public void HasMorePages_ReturnsTrueWhenMorePagesAvailable()
    {
        // Arrange
        var state = new LibraryState
        {
            CurrentPage = 1,
            PageSize = 50,
            TotalCount = 100
        };

        // Assert: 1 * 50 = 50 < 100
        Assert.True(state.HasMorePages);
    }

    [Fact]
    public void HasMorePages_ReturnsFalseWhenAllPagesLoaded()
    {
        // Arrange
        var state = new LibraryState
        {
            CurrentPage = 2,
            PageSize = 50,
            TotalCount = 100
        };

        // Assert: 2 * 50 = 100 >= 100
        Assert.False(state.HasMorePages);
    }

    [Fact]
    public void HasMorePages_ReturnsFalseWhenExactlyOnePage()
    {
        // Arrange
        var state = new LibraryState
        {
            CurrentPage = 1,
            PageSize = 50,
            TotalCount = 50
        };

        // Assert: 1 * 50 = 50 >= 50
        Assert.False(state.HasMorePages);
    }

    [Fact]
    public void ReduceResetPagination_ClearsAllPaginationState()
    {
        // Arrange: State with songs, page 3, search query
        var song1 = new Song { Id = Guid.NewGuid(), Artist = "Test", Title = "Song 1", Mp3FileName = "test1.mp3", CdgFileName = "test1.cdg" };
        var song2 = new Song { Id = Guid.NewGuid(), Artist = "Test", Title = "Song 2", Mp3FileName = "test2.mp3", CdgFileName = "test2.cdg" };
        var initialState = new LibraryState
        {
            Songs = new List<Song> { song1, song2 },
            CurrentPage = 3,
            TotalCount = 150,
            ServerSearchQuery = "test query",
            PageSize = 50
        };

        // Act: Reset pagination
        var action = new ResetPaginationAction();
        var newState = LibraryReducers.ReduceResetPagination(initialState, action);

        // Assert: All pagination state reset
        Assert.Equal(1, newState.CurrentPage);
        Assert.Equal(0, newState.TotalCount);
        Assert.Empty(newState.Songs);
        Assert.Null(newState.ServerSearchQuery);
        Assert.Equal(50, newState.PageSize); // PageSize should remain unchanged
    }

    [Fact]
    public void ReduceLoadPageAction_SetsIsLoadingTrue()
    {
        // Arrange
        var initialState = new LibraryState
        {
            IsLoading = false
        };

        // Act
        var action = new LoadPageAction(Page: 1, SearchQuery: null, Append: false);
        var newState = LibraryReducers.ReduceLoadPageAction(initialState, action);

        // Assert
        Assert.True(newState.IsLoading);
    }

    [Fact]
    public async Task Effect_HandleLoadPageAction_CallsSessionService()
    {
        // Arrange: Mock SessionApiClient and State
        var mockSessionApiClient = new Mock<ISessionApiClient>();
        var mockState = new Mock<IState<Store.Session.SessionState>>();
        var mockLibraryState = new Mock<IState<LibraryState>>();
        var mockDispatcher = new Mock<IDispatcher>();

        var sessionId = Guid.NewGuid();
        var sessionState = new Store.Session.SessionState
        {
            CurrentSession = new Models.Session { SessionId = sessionId }
        };
        mockState.Setup(s => s.Value).Returns(sessionState);
        
        var libraryState = new LibraryState { PageSize = 50 };
        mockLibraryState.Setup(s => s.Value).Returns(libraryState);

        // Mock FetchLibraryPageAsync to return a valid response
        var song1 = new Song { Id = Guid.NewGuid(), Artist = "Test Artist", Title = "Test Song", Mp3FileName = "test.mp3", CdgFileName = "test.cdg" };
        var responseJson = JsonDocument.Parse($$"""
        {
            "items": [
                {
                    "id": "{{song1.Id}}",
                    "artist": "{{song1.Artist}}",
                    "title": "{{song1.Title}}",
                    "metadataJson": "{}"
                }
            ],
            "totalCount": 150,
            "page": 2,
            "pageSize": 50
        }
        """);
        mockSessionApiClient.Setup(s => s.FetchLibraryPageAsync(sessionId, 2, 50, "test", null))
            .ReturnsAsync(responseJson.RootElement);

        // Create effect
        var effect = new LibraryEffects(mockSessionApiClient.Object, mockState.Object, mockLibraryState.Object);

        // Act: Dispatch LoadPageAction
        var action = new LoadPageAction(Page: 2, SearchQuery: "test", Append: true);
        await effect.HandleLoadPageAction(action, mockDispatcher.Object);

        // Assert: SessionApiClient was called with correct params
        mockSessionApiClient.Verify(
            s => s.FetchLibraryPageAsync(sessionId, 2, 50, "test", null),
            Times.Once
        );

        // Assert: LoadPageSuccessAction was dispatched
        mockDispatcher.Verify(
            d => d.Dispatch(It.Is<LoadPageSuccessAction>(a => 
                a.Page == 2 && 
                a.TotalCount == 150 && 
                a.SearchQuery == "test" && 
                a.Append == true &&
                a.Songs.Count == 1
            )),
            Times.Once
        );
    }

    [Fact]
    public async Task Effect_HandleLoadPageAction_DispatchesFailureOnError()
    {
        // Arrange
        var mockSessionApiClient = new Mock<ISessionApiClient>();
        var mockState = new Mock<IState<Store.Session.SessionState>>();
        var mockLibraryState = new Mock<IState<LibraryState>>();
        var mockDispatcher = new Mock<IDispatcher>();

        var sessionId = Guid.NewGuid();
        var sessionState = new Store.Session.SessionState
        {
            CurrentSession = new Models.Session { SessionId = sessionId }
        };
        mockState.Setup(s => s.Value).Returns(sessionState);
        
        var libraryState = new LibraryState { PageSize = 50 };
        mockLibraryState.Setup(s => s.Value).Returns(libraryState);

        // Mock FetchLibraryPageAsync to throw exception
        mockSessionApiClient.Setup(s => s.FetchLibraryPageAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ThrowsAsync(new Exception("Network error"));

        // Create effect
        var effect = new LibraryEffects(mockSessionApiClient.Object, mockState.Object, mockLibraryState.Object);

        // Act: Dispatch LoadPageAction
        var action = new LoadPageAction(Page: 1, SearchQuery: null, Append: false);
        await effect.HandleLoadPageAction(action, mockDispatcher.Object);

        // Assert: LoadLibraryFailureAction was dispatched
        mockDispatcher.Verify(
            d => d.Dispatch(It.Is<LoadLibraryFailureAction>(a => 
                a.ErrorMessage.Contains("Network error")
            )),
            Times.Once
        );
    }
}
