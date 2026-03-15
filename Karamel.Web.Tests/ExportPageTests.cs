using Bunit;
using Karamel.Web.Contracts;
using Karamel.Web.Helpers;
using Karamel.Web.Models;
using Karamel.Web.Pages;
using Microsoft.JSInterop;
using Xunit;

namespace Karamel.Web.Tests;

/// <summary>
/// bUnit component tests for Export.razor and xUnit unit tests for CsvExportHelper.
/// </summary>
public class ExportPageTests : TestContext
{
    // ── Phase 3: Page shell (US4) ────────────────────────────────────────────

    [Fact]
    public void Export_RendersWithoutError_WithNoSessionParameter()
    {
        // Arrange — set up the JS module for exportBridge
        var moduleInterop = JSInterop.SetupModule("./js/exportBridge.js");
        moduleInterop.SetupVoid("triggerDownload", _ => true);

        // Act — render without any session parameter (simulates direct URL navigation)
        var cut = RenderComponent<Export>();

        // Assert — page title rendered and no session error
        Assert.Contains("Export", cut.Markup);
        var dangerAlerts = cut.FindAll(".alert.alert-danger");
        Assert.Empty(dangerAlerts);
    }

    [Fact]
    public void Export_SelectFolderButton_IsPresentOnInitialRender()
    {
        // Arrange
        JSInterop.SetupModule("./js/exportBridge.js");

        // Act
        var cut = RenderComponent<Export>();

        // Assert
        var button = cut.Find("button.select-folder-btn");
        Assert.NotNull(button);
        Assert.Contains("Select Folder", button.TextContent);
    }

    [Fact]
    public void Export_DownloadButtons_AreAbsentBeforeScan()
    {
        // Arrange
        JSInterop.SetupModule("./js/exportBridge.js");

        // Act
        var cut = RenderComponent<Export>();

        // Assert — download button section is not rendered
        var downloadSection = cut.FindAll(".download-buttons");
        Assert.Empty(downloadSection);
    }

    [Fact]
    public async Task Export_AfterSuccessfulScan_ButtonReflectsSongCount()
    {
        // Arrange — successful scan returning one song
        var moduleInterop = JSInterop.SetupModule("./js/exportBridge.js");
        var songDtos = new[]
        {
            new SongDto("00000000-0000-0000-0000-000000000001", "Queen", "Bohemian Rhapsody",
                "Queen - Bohemian Rhapsody.mp3", "Queen - Bohemian Rhapsody.cdg",
                null, null, "mp3cdg", "Rock/Queen", "Rock/Queen/Queen - Bohemian Rhapsody",
                "directory", null, null, null, null, null, 354)
        };
        moduleInterop.Setup<SongDto[]>("scanDirectory", _ => true).SetResult(songDtos);
        moduleInterop.SetupVoid("triggerDownload", _ => true);

        var cut = RenderComponent<Export>();
        await Task.Delay(50);
        cut.Render();

        // Act — click Select Folder button
        await cut.Find("button.select-folder-btn").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Assert — button reflects scan result ("1 songs")
        var button = cut.Find("button.select-folder-btn");
        Assert.Contains("songs", button.TextContent);
        Assert.Contains("1", button.TextContent);
    }

    [Fact]
    public async Task Export_ShowsErrorMessage_WhenScanErrors()
    {
        // Arrange — make scanDirectory throw a JSException
        var moduleInterop = JSInterop.SetupModule("./js/exportBridge.js");
        moduleInterop.Setup<object[]>("scanDirectory", _ => true)
                     .SetException(new JSException("User cancelled directory selection"));

        var cut = RenderComponent<Export>();

        // Trigger OnAfterRenderAsync to load module
        await Task.Delay(50);
        cut.Render();

        // Act — click select folder button
        await cut.Find("button.select-folder-btn").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Assert — error alert displayed
        var dangerAlert = cut.Find(".alert.alert-danger");
        Assert.Contains("Scan failed", dangerAlert.TextContent);
    }

    [Fact]
    public async Task Export_ShowsDownloadButtonSection_AfterSuccessfulScan()
    {
        // Arrange — successful scan returning an empty song list
        var moduleInterop = JSInterop.SetupModule("./js/exportBridge.js");
        moduleInterop.Setup<SongDto[]>("scanDirectory", _ => true)
                     .SetResult(Array.Empty<SongDto>());
        moduleInterop.SetupVoid("triggerDownload", _ => true);

        var cut = RenderComponent<Export>();
        await Task.Delay(50);
        cut.Render();

        // Act
        await cut.Find("button.select-folder-btn").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Assert — download button section visible after successful scan
        var downloadSection = cut.Find(".download-buttons");
        Assert.NotNull(downloadSection);
    }

    // ── Phase 4: GenerateArtistsCsv (US1) ────────────────────────────────────

    [Fact]
    public void GenerateArtistsCsv_HeaderRowPresent()
    {
        var csv = CsvExportHelper.GenerateArtistsCsv(Array.Empty<Song>());
        Assert.StartsWith("Artist;Title\n", csv);
    }

    [Fact]
    public void GenerateArtistsCsv_EmptySongList_YieldsHeaderOnly()
    {
        var csv = CsvExportHelper.GenerateArtistsCsv(Array.Empty<Song>());
        Assert.Equal("Artist;Title\n", csv);
    }

    [Fact]
    public void GenerateArtistsCsv_SortedAlphabetically_CaseInsensitive()
    {
        var songs = new[]
        {
            MakeSong("ZZ Top", "Sharp Dressed Man"),
            MakeSong("abba", "Dancing Queen"),
            MakeSong("Queen", "Bohemian Rhapsody"),
        };

        var csv = CsvExportHelper.GenerateArtistsCsv(songs);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Lines 1-3 (after header) should be sorted by artist case-insensitively
        Assert.Equal("Artist;Title", lines[0]);
        var artists = lines.Skip(1).Select(l => l.Split(';')[0]).ToList();

        // abba < Queen < ZZ Top (Ordinal compare of lowercased keys: "abba" < "queen" < "zz top")
        Assert.Equal("abba", artists[0]);
        Assert.Equal("Queen", artists[1]);
        Assert.Equal("ZZ Top", artists[2]);
    }

    [Fact]
    public void GenerateArtistsCsv_DigitsAndSpecialsBefore_AToZ()
    {
        var songs = new[]
        {
            MakeSong("Abba", "Song"),
            MakeSong("!T.O.O.H.!", "Song"),
            MakeSong("10cc", "Song"),
        };

        var csv = CsvExportHelper.GenerateArtistsCsv(songs);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToList();
        var artists = lines.Select(l => l.Split(';')[0]).ToList();

        // Ordinal sort: '!' (0x21) < '1' (0x31) < 'a' (0x61)
        Assert.Equal("!T.O.O.H.!", artists[0]);
        Assert.Equal("10cc", artists[1]);
        Assert.Equal("Abba", artists[2]);
    }

    [Fact]
    public void GenerateArtistsCsv_FieldWithSemicolon_IsQuoted()
    {
        var songs = new[]
        {
            MakeSong("Artist;With;Semicolons", "Title"),
        };

        var csv = CsvExportHelper.GenerateArtistsCsv(songs);
        Assert.Contains("\"Artist;With;Semicolons\"", csv);
    }

    [Fact]
    public void GenerateArtistsCsv_NullArtist_TreatedAsEmpty()
    {
        var song = new Song
        {
            Id = Guid.NewGuid(),
            Artist = null!,
            Title = "Test Title",
            Mp3FileName = "test.mp3",
            CdgFileName = "test.cdg"
        };

        var csv = CsvExportHelper.GenerateArtistsCsv(new[] { song });
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        // Artist column should be empty string
        Assert.Equal(";Test Title", lines[1]);
    }

    // ── Phase 5: GenerateTitlesCsv (US2) ─────────────────────────────────────

    [Fact]
    public void GenerateTitlesCsv_HeaderRowPresent()
    {
        var csv = CsvExportHelper.GenerateTitlesCsv(Array.Empty<Song>());
        Assert.StartsWith("Title;Artist\n", csv);
    }

    [Fact]
    public void GenerateTitlesCsv_EmptySongList_YieldsHeaderOnly()
    {
        var csv = CsvExportHelper.GenerateTitlesCsv(Array.Empty<Song>());
        Assert.Equal("Title;Artist\n", csv);
    }

    [Fact]
    public void GenerateTitlesCsv_SortedAlphabeticallyByTitle_CaseInsensitive()
    {
        var songs = new[]
        {
            MakeSong("Artist1", "Zebra Song"),
            MakeSong("Artist2", "apple song"),
            MakeSong("Artist3", "Midnight Express"),
        };

        var csv = CsvExportHelper.GenerateTitlesCsv(songs);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("Title;Artist", lines[0]);
        var titles = lines.Skip(1).Select(l => l.Split(';')[0]).ToList();

        // apple song < Midnight Express < Zebra Song (Ordinal compare of lowercased)
        Assert.Equal("apple song", titles[0]);
        Assert.Equal("Midnight Express", titles[1]);
        Assert.Equal("Zebra Song", titles[2]);
    }

    [Fact]
    public void GenerateTitlesCsv_FieldWithSemicolon_IsQuoted()
    {
        var songs = new[]
        {
            MakeSong("Artist", "Title;With;Semicolons"),
        };

        var csv = CsvExportHelper.GenerateTitlesCsv(songs);
        Assert.Contains("\"Title;With;Semicolons\"", csv);
    }

    [Fact]
    public void GenerateTitlesCsv_NullTitle_TreatedAsEmpty()
    {
        var song = new Song
        {
            Id = Guid.NewGuid(),
            Artist = "Test Artist",
            Title = null!,
            Mp3FileName = "test.mp3",
            CdgFileName = "test.cdg"
        };

        var csv = CsvExportHelper.GenerateTitlesCsv(new[] { song });
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        // Title column should be empty string, Artist follows
        Assert.Equal(";Test Artist", lines[1]);
    }

    [Fact]
    public void GenerateTitlesCsv_IdenticalTitles_DifferentArtists_AppearConsecutively()
    {
        var songs = new[]
        {
            MakeSong("ZZ Top", "Sharp Dressed Man"),
            MakeSong("Artist A", "Hello"),
            MakeSong("Artist B", "Hello"),
        };

        var csv = CsvExportHelper.GenerateTitlesCsv(songs);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToList();
        // Both "Hello" rows should appear before "Sharp Dressed Man"
        Assert.Equal("Hello", lines[0].Split(';')[0]);
        Assert.Equal("Hello", lines[1].Split(';')[0]);
        Assert.Equal("Sharp Dressed Man", lines[2].Split(';')[0]);
    }

    // ── Phase 6: Duplicates (US3) ─────────────────────────────────────────────

    [Fact]
    public void GenerateDuplicatesCsv_HeaderPresent()
    {
        var csv = CsvExportHelper.GenerateDuplicatesCsv(Array.Empty<Song>());
        Assert.StartsWith("Artist;Title;FilePath\n", csv);
    }

    [Fact]
    public void GenerateDuplicatesCsv_NoDuplicates_YieldsHeaderOnly()
    {
        var songs = new[]
        {
            MakeSong("Queen", "Bohemian Rhapsody"),
            MakeSong("ABBA", "Dancing Queen"),
        };

        var csv = CsvExportHelper.GenerateDuplicatesCsv(songs);
        Assert.Equal("Artist;Title;FilePath\n", csv);
    }

    [Fact]
    public void GenerateDuplicatesCsv_ExactDuplicates_ArtistTitleCaseInsensitive_Detected()
    {
        // "queen" vs "Queen" — same after normalization
        var songs = new[]
        {
            MakeSong("Queen", "Bohemian Rhapsody"),
            MakeSong("queen", "bohemian rhapsody"),
            MakeSong("ABBA", "Dancing Queen"),
        };

        var csv = CsvExportHelper.GenerateDuplicatesCsv(songs);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToList();

        // 2 duplicate rows — ABBA is not a duplicate
        Assert.Equal(2, lines.Count);
        // Both rows relate to the Rhapsody pair (case-insensitive: titles are "Bohemian Rhapsody" and "bohemian rhapsody")
        Assert.All(lines, l => Assert.Contains("rhapsody", l, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GenerateDuplicatesCsv_NonDuplicates_NotIncluded()
    {
        var songs = new[]
        {
            MakeSong("Queen", "Bohemian Rhapsody"),
            MakeSong("ABBA", "Dancing Queen"),
            MakeSong("Led Zeppelin", "Stairway to Heaven"),
        };

        var csv = CsvExportHelper.GenerateDuplicatesCsv(songs);
        Assert.Equal("Artist;Title;FilePath\n", csv);
    }

    [Fact]
    public void GenerateDuplicatesCsv_ThreeWayExactGroup_AllThreeRowsPresent()
    {
        var songs = new[]
        {
            MakeSong("Queen", "Bohemian Rhapsody"),
            MakeSong("Queen", "Bohemian Rhapsody"),
            MakeSong("Queen", "Bohemian Rhapsody"),
        };

        var csv = CsvExportHelper.GenerateDuplicatesCsv(songs);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToList();

        Assert.Equal(3, lines.Count);
    }

    [Fact]
    public void GenerateDuplicatesCsv_LikelyDuplicates_WithinThreshold_Detected()
    {
        // Artist OSA=1, title OSA=1 — within thresholds (2, 3)
        var songs = new[]
        {
            MakeSong("Queen", "Bohemian Rhapsody"),
            MakeSong("Quean", "Bohemian Rhapsodx"),
        };

        var csv = CsvExportHelper.GenerateDuplicatesCsv(songs);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToList();

        Assert.Equal(2, lines.Count);
    }

    [Fact]
    public void GenerateDuplicatesCsv_LikelyDuplicates_ExceedingThreshold_NotIncluded()
    {
        var songs = new[]
        {
            MakeSong("Queen", "Bohemian Rhapsody"),
            MakeSong("Madonna", "Like a Prayer"),
        };

        var csv = CsvExportHelper.GenerateDuplicatesCsv(songs);
        Assert.Equal("Artist;Title;FilePath\n", csv);
    }

    [Fact]
    public void GenerateDuplicatesCsv_ExactDuplicateSongs_ExcludedFromLikelyCandidates()
    {
        // Two exact duplicates — they should NOT also appear in the likely groups
        var songs = new[]
        {
            MakeSong("Queen", "Bohemian Rhapsody"),
            MakeSong("queen", "bohemian rhapsody"), // exact duplicate after normalization
        };

        var csv = CsvExportHelper.GenerateDuplicatesCsv(songs);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToList();

        // Exactly 2 rows — the exact pair — no extra rows from likely processing
        Assert.Equal(2, lines.Count);
    }

    [Fact]
    public void GenerateDuplicatesCsv_ExactGroupsBeforeLikelyGroups()
    {
        // Exact duplicate pair
        var exactA = MakeSong("Queen", "Bohemian Rhapsody", "/music/queen/boh1.mp3");
        var exactB = MakeSong("queen", "bohemian rhapsody", "/music/queen/boh2.mp3");
        // Likely duplicate pair (artist off by 1, title off by 1)
        var likelyA = MakeSong("Adele", "Rolling in the Deepx", "/music/adele/deep1.mp3");
        var likelyB = MakeSong("Adele", "Rolling in the Deep", "/music/adele/deep2.mp3");

        var songs = new[] { exactA, exactB, likelyA, likelyB };
        var csv = CsvExportHelper.GenerateDuplicatesCsv(songs);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToList();

        // First two rows: exact group (Bohemian Rhapsody — original casing preserved per song)
        Assert.Contains("rhapsody", lines[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rhapsody", lines[1], StringComparison.OrdinalIgnoreCase);
        // Last two rows: likely group (Adele / Rolling in the Deep)
        Assert.Contains("Adele", lines[2]);
        Assert.Contains("Adele", lines[3]);
    }

    [Fact]
    public void GenerateDuplicatesCsv_FilePathColumn_Populated_FromFullPath()
    {
        var song1 = MakeSong("Queen", "Bohemian Rhapsody", "/music/boh1.mp3");
        var song2 = MakeSong("Queen", "Bohemian Rhapsody", "/music/boh2.mp3");

        var csv = CsvExportHelper.GenerateDuplicatesCsv(new[] { song1, song2 });
        Assert.Contains("/music/boh1.mp3", csv);
        Assert.Contains("/music/boh2.mp3", csv);
    }

    [Fact]
    public void GenerateDuplicatesCsv_EmptySongList_YieldsHeaderOnly()
    {
        var csv = CsvExportHelper.GenerateDuplicatesCsv(Array.Empty<Song>());
        Assert.Equal("Artist;Title;FilePath\n", csv);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static Song MakeSong(string artist, string title, string? fullPath = null) => new Song
    {
        Id = Guid.NewGuid(),
        Artist = artist,
        Title = title,
        Mp3FileName = "test.mp3",
        CdgFileName = "test.cdg",
        FullPath = fullPath
    };
}
