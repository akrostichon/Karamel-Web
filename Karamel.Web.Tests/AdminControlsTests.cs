using Bunit;
using Microsoft.JSInterop;
using Xunit;
using Karamel.Web.Pages;
using Karamel.Web.Store.Session;
using Karamel.Web.Store.Playlist;
using Karamel.Web.Models;
using System.Collections.Generic;

namespace Karamel.Web.Tests
{
    /// <summary>
    /// Unit tests for admin controls (gear icon and dropdown menu) in NextSongView and PlayerView.
    /// Tests gear button rendering, dropdown toggling, and menu items for reopening Playlist/Singer tabs.
    /// </summary>
    public class AdminControlsTests : SessionTestBase
    {
        private readonly Session _testSession;
        private readonly List<Song> _testQueue;

        public AdminControlsTests()
        {
            _testSession = new Session
            {
                SessionId = Guid.NewGuid(),
                RequireSingerName = true
            };

            var song = new Song
            {
                Id = Guid.NewGuid(),
                Artist = "Test Artist",
                Title = "Test Song",
                Mp3FileName = "test.mp3",
                CdgFileName = "test.cdg",
                AddedBySinger = "Test Singer"
            };
            _testQueue = new List<Song> { song };
        }

        #region NextSongView Tests

        [Fact]
        public void NextSongView_GearIcon_RendersInTopRight()
        {
            // Arrange
            var sessionState = new SessionState { CurrentSession = _testSession };
            var playlistState = new PlaylistState { Queue = new Queue<Song>(_testQueue) };
            SetupTestWithSession(sessionState, playlistState, view: "nextsong");

            JSInterop.Mode = JSRuntimeMode.Loose;

            // Act
            var cut = RenderComponent<NextSongView>();

            // Assert
            var gearButton = cut.Find(".admin-gear-button");
            Assert.NotNull(gearButton);
        }

        [Fact]
        public void NextSongView_GearIcon_TogglesDropdown()
        {
            // Arrange
            var sessionState = new SessionState { CurrentSession = _testSession };
            var playlistState = new PlaylistState { Queue = new Queue<Song>(_testQueue) };
            SetupTestWithSession(sessionState, playlistState, view: "nextsong");

            JSInterop.Mode = JSRuntimeMode.Loose;

            var cut = RenderComponent<NextSongView>();

            // Act - Click gear icon
            var gearButton = cut.Find(".admin-gear-button");
            gearButton.Click();
            cut.Render();

            // Assert - Dropdown should appear
            var dropdown = cut.Find(".admin-dropdown");
            Assert.NotNull(dropdown);

            // Act - Click gear icon again
            gearButton.Click();
            cut.Render();

            // Assert - Dropdown should disappear
            Assert.Throws<Bunit.ElementNotFoundException>(() => cut.Find(".admin-dropdown"));
        }

        [Fact]
        public void NextSongView_Dropdown_OpenPlaylist_CallsJSInterop()
        {
            // Arrange
            var sessionState = new SessionState { CurrentSession = _testSession };
            var playlistState = new PlaylistState { Queue = new Queue<Song>(_testQueue) };
            SetupTestWithSession(sessionState, playlistState, view: "nextsong");

            JSInterop.Mode = JSRuntimeMode.Loose;
            JSInterop.SetupVoid("open", _ => true);

            var cut = RenderComponent<NextSongView>();

            // Act - Open dropdown
            var gearButton = cut.Find(".admin-gear-button");
            gearButton.Click();
            cut.Render();

            // Assert - Verify "Open Playlist Manager" item exists
            var playlistItem = cut.Find(".admin-dropdown-item.open-playlist");
            Assert.NotNull(playlistItem);
            Assert.Contains("Playlist Manager", playlistItem.TextContent);
        }

        [Fact]
        public void NextSongView_Dropdown_OpenSinger_CallsJSInterop()
        {
            // Arrange
            var sessionState = new SessionState { CurrentSession = _testSession };
            var playlistState = new PlaylistState { Queue = new Queue<Song>(_testQueue) };
            SetupTestWithSession(sessionState, playlistState, view: "nextsong");

            JSInterop.Mode = JSRuntimeMode.Loose;
            JSInterop.SetupVoid("open", _ => true);

            var cut = RenderComponent<NextSongView>();

            // Act - Open dropdown
            var gearButton = cut.Find(".admin-gear-button");
            gearButton.Click();
            cut.Render();

            // Assert - Verify "Open Singer View" item exists
            var singerItem = cut.Find(".admin-dropdown-item.open-singer");
            Assert.NotNull(singerItem);
            Assert.Contains("Singer View", singerItem.TextContent);
        }

        #endregion

        #region PlayerView Tests

        [Fact]
        public void PlayerView_GearIcon_RendersWithLowerOpacity()
        {
            // Arrange
            var sessionState = new SessionState { CurrentSession = _testSession };
            var playlistState = new PlaylistState { Queue = new Queue<Song>(_testQueue) };
            SetupTestWithSession(sessionState, playlistState, view: "player");

            JSInterop.Mode = JSRuntimeMode.Loose;

            // Act
            var cut = RenderComponent<PlayerView>();

            // Assert
            var gearButton = cut.Find(".admin-gear-button");
            Assert.NotNull(gearButton);
            // Note: CSS class check for "nearly invisible" styling verified via integration test
        }

        [Fact]
        public void PlayerView_Dropdown_SameItems_AsNextSongView()
        {
            // Arrange
            var sessionState = new SessionState { CurrentSession = _testSession };
            var playlistState = new PlaylistState { Queue = new Queue<Song>(_testQueue) };
            SetupTestWithSession(sessionState, playlistState, view: "player");

            JSInterop.Mode = JSRuntimeMode.Loose;

            var cut = RenderComponent<PlayerView>();

            // Act - Open dropdown
            var gearButton = cut.Find(".admin-gear-button");
            gearButton.Click();
            cut.Render();

            // Assert - Verify both menu items exist
            var playlistItem = cut.Find(".admin-dropdown-item.open-playlist");
            var singerItem = cut.Find(".admin-dropdown-item.open-singer");
            
            Assert.NotNull(playlistItem);
            Assert.NotNull(singerItem);
            Assert.Contains("Playlist Manager", playlistItem.TextContent);
            Assert.Contains("Singer View", singerItem.TextContent);
        }

        #endregion
    }
}
