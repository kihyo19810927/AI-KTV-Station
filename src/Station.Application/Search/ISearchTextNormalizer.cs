using System.Text;

namespace Station.Application.Search;

public interface ISearchTextNormalizer
{
    SearchTextKeys CreateKeys(string value);
}

public sealed record SearchTextKeys(
    string Normalized,
    string Simplified,
    string Traditional,
    string Pinyin,
    string Initials,
    string Compact);

public sealed class InvariantSearchTextNormalizer : ISearchTextNormalizer
{
    public SearchTextKeys CreateKeys(string value)
    {
        var normalized = SearchTextNormalization.Normalize(value);
        return new SearchTextKeys(normalized, normalized, normalized, string.Empty, string.Empty, SearchTextNormalization.Compact(normalized));
    }
}

public static class SearchTextNormalization
{
    public static string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var normalized = value.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        var output = new StringBuilder(normalized.Length);
        var pendingSpace = false;
        foreach (var rune in normalized.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
            {
                pendingSpace = output.Length > 0;
                continue;
            }
            if (pendingSpace) output.Append(' ');
            output.Append(rune.ToString());
            pendingSpace = false;
        }
        return output.ToString();
    }

    public static string Compact(string value)
    {
        var output = new StringBuilder(value.Length);
        foreach (var rune in value.EnumerateRunes())
            if (Rune.IsLetterOrDigit(rune)) output.Append(rune.ToString());
        return output.ToString();
    }
}
