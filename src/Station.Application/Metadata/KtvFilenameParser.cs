using System.Text;
using System.Text.RegularExpressions;

namespace Station.Application.Metadata;

public sealed partial class KtvFilenameParser : IMediaFilenameParser
{
    private static readonly HashSet<string> Languages = new(StringComparer.OrdinalIgnoreCase) { "国语", "粤语", "英语", "日语", "韩语", "闽南语", "客家语", "纯音乐" };
    private static readonly HashSet<string> Categories = new(StringComparer.OrdinalIgnoreCase) { "流行", "流行歌曲", "合唱", "儿歌", "民歌", "摇滚", "经典", "舞曲" };
    private static readonly HashSet<string> Versions = new(StringComparer.OrdinalIgnoreCase) { "MTV", "繁体版", "简体版", "现场版", "演唱会版", "伴奏版" };

    public ParsedSongMetadata Parse(string relativePath)
    {
        var originalStem = Path.GetFileNameWithoutExtension(relativePath);
        var stem = originalStem.Normalize(NormalizationForm.FormKC).Trim();
        var parts = SeparatorRegex().Split(stem).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
        string? language = null, category = null;
        while (parts.Count > 0)
        {
            var candidate = parts[^1];
            if (language is null && Languages.Contains(candidate)) language = RemoveLast(parts);
            else if (category is null && Categories.Contains(candidate)) category = RemoveLast(parts);
            else break;
        }

        var core = string.Join('-', parts);
        string? quality = null;
        var qualityMatch = QualityRegex().Match(core);
        if (qualityMatch.Success) { quality = qualityMatch.Groups["value"].Value.Trim(); core = QualityRegex().Replace(core, string.Empty).Trim(); }
        string? version = null;
        var versionMatch = VersionRegex().Match(core);
        if (versionMatch.Success && Versions.Contains(versionMatch.Groups["value"].Value.Trim()))
        {
            version = versionMatch.Groups["value"].Value.Trim();
            core = core[..versionMatch.Index].Trim();
        }

        var warnings = new List<string>();
        var split = core.IndexOf('-');
        if (split <= 0 || split == core.Length - 1)
        {
            warnings.Add("metadata.artist_missing");
            return new ParsedSongMetadata(originalStem, string.IsNullOrWhiteSpace(core) ? stem : core, null, [], quality, language, category, version, 0.4, warnings);
        }

        var artistDisplay = core[..split].Trim();
        var title = core[(split + 1)..].Trim();
        var artists = artistDisplay.Split('_', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (artists.Length == 0) warnings.Add("metadata.artist_missing");
        var confidence = language is not null || category is not null || quality is not null ? 0.95 : 0.75;
        return new ParsedSongMetadata(originalStem, title, artistDisplay, artists, quality, language, category, version, confidence, warnings);
    }

    private static string RemoveLast(List<string> parts) { var value = parts[^1]; parts.RemoveAt(parts.Count - 1); return value; }

    [GeneratedRegex(@"\s*[-－–—]\s*")]
    private static partial Regex SeparatorRegex();
    [GeneratedRegex(@"[\[【](?<value>[^\]】]+)[\]】]")]
    private static partial Regex QualityRegex();
    [GeneratedRegex(@"[\(（](?<value>[^\)）]+)[\)）]\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex VersionRegex();
}
