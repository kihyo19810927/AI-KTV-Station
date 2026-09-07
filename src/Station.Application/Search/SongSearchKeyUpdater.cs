using Station.Domain.Models;

namespace Station.Application.Search;

public static class SongSearchKeyUpdater
{
    public static bool Update(Song song, ISearchTextNormalizer normalizer)
    {
        var changed = false;
        var title = normalizer.CreateKeys(song.Title);
        changed |= Assign(song.NormalizedTitle, title.Normalized, value => song.NormalizedTitle = value);
        changed |= Assign(song.SimplifiedTitle, title.Simplified, value => song.SimplifiedTitle = value);
        changed |= Assign(song.TraditionalTitle, title.Traditional, value => song.TraditionalTitle = value);
        changed |= Assign(song.TitlePinyin, title.Pinyin, value => song.TitlePinyin = value);
        changed |= Assign(song.TitleInitials, title.Initials, value => song.TitleInitials = value);
        changed |= Assign(song.CompactTitle, title.Compact, value => song.CompactTitle = value);

        foreach (var link in song.Artists)
        {
            var artist = link.Artist;
            var keys = normalizer.CreateKeys(artist.Name);
            changed |= Assign(artist.NormalizedName, keys.Normalized, value => artist.NormalizedName = value);
            changed |= Assign(artist.SimplifiedName, keys.Simplified, value => artist.SimplifiedName = value);
            changed |= Assign(artist.TraditionalName, keys.Traditional, value => artist.TraditionalName = value);
            changed |= Assign(artist.Pinyin, keys.Pinyin, value => artist.Pinyin = value);
            changed |= Assign(artist.Initials, keys.Initials, value => artist.Initials = value);
            changed |= Assign(artist.CompactName, keys.Compact, value => artist.CompactName = value);
        }
        return changed;
    }

    private static bool Assign(string? current, string value, Action<string> setter)
    {
        if (current == value) return false;
        setter(value);
        return true;
    }
}
