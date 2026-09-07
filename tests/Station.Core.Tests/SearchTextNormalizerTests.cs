using Station.Application.Search;
using Station.Infrastructure.Search;

namespace Station.Core.Tests;

public sealed class SearchTextNormalizerTests
{
    [Fact]
    public void Invariant_normalization_handles_unicode_case_whitespace_and_compact_form()
    {
        var keys = new InvariantSearchTextNormalizer().CreateKeys("  Ａ-Lin\t LIVE ２０２６ 🎤  ");

        Assert.Equal("a-lin live 2026 🎤", keys.Normalized);
        Assert.Equal("alinlive2026", keys.Compact);
    }

    [Theory]
    [InlineData("我愛中國", "我爱中国", "我愛中國", "woaizhongguo", "wazg")]
    [InlineData("周杰伦 夜曲", "周杰伦 夜曲", "周杰倫 夜曲", "zhoujielunyequ", "zjlyq")]
    public void ToolGood_normalizer_builds_simplified_traditional_pinyin_and_initial_keys(
        string input,
        string simplified,
        string traditional,
        string pinyin,
        string initials)
    {
        var keys = new ToolGoodSearchTextNormalizer().CreateKeys(input);

        Assert.Equal(SearchTextNormalization.Normalize(input), keys.Normalized);
        Assert.Equal(simplified, keys.Simplified);
        Assert.Equal(traditional, keys.Traditional);
        Assert.Equal(pinyin, keys.Pinyin);
        Assert.Equal(initials, keys.Initials);
    }
}
