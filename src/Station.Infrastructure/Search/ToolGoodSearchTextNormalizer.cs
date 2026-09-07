using Station.Application.Search;
using ToolGood.Words;

namespace Station.Infrastructure.Search;

public sealed class ToolGoodSearchTextNormalizer : ISearchTextNormalizer
{
    public SearchTextKeys CreateKeys(string value)
    {
        var normalized = SearchTextNormalization.Normalize(value);
        var simplified = SearchTextNormalization.Normalize(WordsHelper.ToSimplifiedChinese(normalized, 0));
        var traditional = SearchTextNormalization.Normalize(WordsHelper.ToTraditionalChinese(normalized, 0));
        var pinyin = SearchTextNormalization.Compact(SearchTextNormalization.Normalize(WordsHelper.GetPinyin(simplified, false)));
        var initials = SearchTextNormalization.Compact(SearchTextNormalization.Normalize(WordsHelper.GetFirstPinyin(simplified)));
        return new SearchTextKeys(normalized, simplified, traditional, pinyin, initials, SearchTextNormalization.Compact(normalized));
    }
}
