using System.Globalization;
using System.Text;

namespace Karamel.Backend.Services;

public static class SearchText
{
    public static string Fold(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        try
        {
            var normalized = value.Trim();
            normalized = normalized.Normalize(NormalizationForm.FormD);

            var builder = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                builder.Append(ch);
            }

            var folded = builder.ToString()
                .ToLowerInvariant()
                .Replace("ß", "ss")
                .Replace("æ", "ae")
                .Replace("œ", "oe")
                .Replace("ø", "o")
                .Replace("đ", "d")
                .Replace("ł", "l");

            return folded;
        }
        catch (Exception ex) when (ex is ArgumentException or PlatformNotSupportedException)
        {
            return value.Trim()
                .ToLowerInvariant()
                .Replace("ß", "ss")
                .Replace("æ", "ae")
                .Replace("œ", "oe")
                .Replace("ø", "o")
                .Replace("đ", "d")
                .Replace("ł", "l");
        }
    }
}
