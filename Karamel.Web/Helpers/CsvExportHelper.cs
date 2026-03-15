using System.Text;
using System.Text.RegularExpressions;
using Karamel.Web.Models;

namespace Karamel.Web.Helpers;

public static class CsvExportHelper
{
    public const int ArtistLevenshteinThreshold = 2;
    public const int TitleLevenshteinThreshold = 3;

    /// <summary>
    /// Escapes a CSV field per RFC 4180 using semicolons as delimiters.
    /// Wraps the field in double-quotes if it contains ';', '"', newline, or carriage return,
    /// and escapes embedded double-quotes by doubling them.
    /// </summary>
    internal static string EscapeCsvField(string value)
    {
        if (value.Contains(';') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    /// <summary>
    /// Normalizes a string for duplicate comparison:
    /// lowercase, strip leading articles (the/a/an), strip punctuation (/ - ' " , .), collapse whitespace.
    /// </summary>
    internal static string NormalizeForComparison(string value)
    {
        var v = value.ToLowerInvariant().Trim();
        foreach (var article in new[] { "the ", "a ", "an " })
        {
            if (v.StartsWith(article, StringComparison.Ordinal))
            {
                v = v[article.Length..];
                break;
            }
        }
        v = Regex.Replace(v, @"[/\-'"",.]", "");
        v = Regex.Replace(v, @"\s+", " ").Trim();
        return v;
    }

    /// <summary>
    /// Optimal String Alignment (restricted Damerau-Levenshtein) distance with early-exit.
    /// Returns earlyExitThreshold + 1 if the distance would exceed earlyExitThreshold.
    /// </summary>
    internal static int OsaDistance(string a, string b, int earlyExitThreshold)
    {
        // Early-exit on length difference alone
        if (Math.Abs(a.Length - b.Length) > earlyExitThreshold)
            return earlyExitThreshold + 1;

        int lenA = a.Length;
        int lenB = b.Length;

        var d = new int[lenA + 1, lenB + 1];

        for (int i = 0; i <= lenA; i++) d[i, 0] = i;
        for (int j = 0; j <= lenB; j++) d[0, j] = j;

        for (int i = 1; i <= lenA; i++)
        {
            int rowMin = int.MaxValue;
            for (int j = 1; j <= lenB; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);

                // Transposition
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                    d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + cost);

                if (d[i, j] < rowMin) rowMin = d[i, j];
            }
            // If the minimum value in this row already exceeds the threshold, bail out early
            if (rowMin > earlyExitThreshold)
                return earlyExitThreshold + 1;
        }

        return d[lenA, lenB] > earlyExitThreshold ? earlyExitThreshold + 1 : d[lenA, lenB];
    }

    /// <summary>
    /// Generates artists.csv content: header Artist;Title, rows sorted by Artist ascending (case-insensitive, Ordinal).
    /// </summary>
    public static string GenerateArtistsCsv(IEnumerable<Song> songs)
    {
        var sb = new StringBuilder();
        sb.Append("Artist;Title\n");
        foreach (var s in songs.OrderBy(s => (s.Artist ?? "").ToLowerInvariant(), StringComparer.Ordinal))
        {
            sb.Append(EscapeCsvField(s.Artist ?? ""));
            sb.Append(';');
            sb.Append(EscapeCsvField(s.Title ?? ""));
            sb.Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>
    /// Generates titles.csv content: header Title;Artist, rows sorted by Title ascending (case-insensitive, Ordinal).
    /// </summary>
    public static string GenerateTitlesCsv(IEnumerable<Song> songs)
    {
        var sb = new StringBuilder();
        sb.Append("Title;Artist\n");
        foreach (var s in songs.OrderBy(s => (s.Title ?? "").ToLowerInvariant(), StringComparer.Ordinal))
        {
            sb.Append(EscapeCsvField(s.Title ?? ""));
            sb.Append(';');
            sb.Append(EscapeCsvField(s.Artist ?? ""));
            sb.Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>
    /// Finds groups of exact duplicates (≥2 songs sharing the same normalized Artist|Title key).
    /// </summary>
    internal static IReadOnlyList<IReadOnlyList<Song>> FindExactDuplicateGroups(IEnumerable<Song> songs)
    {
        var groups = new Dictionary<string, List<Song>>(StringComparer.Ordinal);
        foreach (var song in songs)
        {
            var key = NormalizeForComparison(song.Artist ?? "") + "|" + NormalizeForComparison(song.Title ?? "");
            if (!groups.TryGetValue(key, out var list))
            {
                list = new List<Song>();
                groups[key] = list;
            }
            list.Add(song);
        }
        return groups.Values
            .Where(g => g.Count >= 2)
            .Select(g => (IReadOnlyList<Song>)g)
            .ToList();
    }

    /// <summary>
    /// Finds groups of likely duplicates (≥2 songs within Levenshtein thresholds, excluding exact duplicates).
    /// Uses Union-Find clustering for transitive grouping.
    /// </summary>
    internal static IReadOnlyList<IReadOnlyList<Song>> FindLikelyDuplicateGroups(
        IEnumerable<Song> songs,
        IReadOnlyCollection<Guid> exactDuplicateSongIds)
    {
        var candidates = songs.Where(s => !exactDuplicateSongIds.Contains(s.Id)).ToList();
        int n = candidates.Count;

        // Pre-compute normalized strings once (O(n)) to avoid O(n²) regex calls inside the pair loop.
        var normArtists = new string[n];
        var normTitles = new string[n];
        for (int k = 0; k < n; k++)
        {
            normArtists[k] = NormalizeForComparison(candidates[k].Artist ?? "");
            normTitles[k] = NormalizeForComparison(candidates[k].Title ?? "");
        }

        // Union-Find parent array
        var parent = Enumerable.Range(0, n).ToArray();

        int Find(int x)
        {
            while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
            return x;
        }
        void Union(int x, int y) { parent[Find(x)] = Find(y); }

        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                // Four-step early-exit using pre-normalized values
                if (Math.Abs(normArtists[i].Length - normArtists[j].Length) > ArtistLevenshteinThreshold) continue;
                if (OsaDistance(normArtists[i], normArtists[j], ArtistLevenshteinThreshold) > ArtistLevenshteinThreshold) continue;

                if (Math.Abs(normTitles[i].Length - normTitles[j].Length) > TitleLevenshteinThreshold) continue;
                if (OsaDistance(normTitles[i], normTitles[j], TitleLevenshteinThreshold) > TitleLevenshteinThreshold) continue;

                Union(i, j);
            }
        }

        // Group by root
        var clusterMap = new Dictionary<int, List<Song>>();
        for (int i = 0; i < n; i++)
        {
            int root = Find(i);
            if (!clusterMap.TryGetValue(root, out var list))
            {
                list = new List<Song>();
                clusterMap[root] = list;
            }
            list.Add(candidates[i]);
        }

        return clusterMap.Values
            .Where(g => g.Count >= 2)
            .Select(g => (IReadOnlyList<Song>)g)
            .ToList();
    }

    /// <summary>
    /// Generates duplicates.csv content: header Artist;Title;FilePath,
    /// exact duplicate groups first (consecutive), then likely duplicate groups (consecutive).
    /// </summary>
    public static string GenerateDuplicatesCsv(IEnumerable<Song> songs)
    {
        var songList = songs.ToList();
        var exactGroups = FindExactDuplicateGroups(songList);
        var exactIds = new HashSet<Guid>(exactGroups.SelectMany(g => g).Select(s => s.Id));
        var likelyGroups = FindLikelyDuplicateGroups(songList, exactIds);

        var sb = new StringBuilder();
        sb.Append("Artist;Title;FilePath\n");

        foreach (var group in exactGroups)
        {
            foreach (var song in group)
            {
                sb.Append(EscapeCsvField(song.Artist ?? ""));
                sb.Append(';');
                sb.Append(EscapeCsvField(song.Title ?? ""));
                sb.Append(';');
                sb.Append(EscapeCsvField(song.FullPath ?? ""));
                sb.Append('\n');
            }
        }

        foreach (var group in likelyGroups)
        {
            foreach (var song in group)
            {
                sb.Append(EscapeCsvField(song.Artist ?? ""));
                sb.Append(';');
                sb.Append(EscapeCsvField(song.Title ?? ""));
                sb.Append(';');
                sb.Append(EscapeCsvField(song.FullPath ?? ""));
                sb.Append('\n');
            }
        }

        return sb.ToString();
    }
}
