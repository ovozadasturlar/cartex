using Cartex.Shared.Search;
using Xunit;

namespace Cartex.UnitTests;

public sealed class SearchFoldTests
{
    [Theory]
    [InlineData("Қора")]
    [InlineData("Кора")]
    [InlineData("Qora")]
    [InlineData("Kora")]
    public void WP10_Qora_variants_have_the_same_fuzzy_skeleton(string value)
    {
        Assert.Equal("kora", SearchFold.Fuzzy(value));
    }

    [Theory]
    [InlineData("Ғишт", "Gisht")]
    [InlineData("Ҳовли", "Hovli")]
    [InlineData("Ўрик", "Orik")]
    [InlineData("Ўрик", "O'rik")]
    [InlineData("Чойшаб", "Choyshab")]
    [InlineData("Шакар", "Shakar")]
    [InlineData("Ёғ", "Yog'")]
    public void WP10_Cyrillic_and_Latin_words_have_the_same_fuzzy_skeleton(string cyrillic, string latin)
    {
        Assert.Equal(SearchFold.Fuzzy(latin), SearchFold.Fuzzy(cyrillic));
    }

    [Fact]
    public void WP10_Strict_preserves_distinguishing_letters()
    {
        Assert.Equal("qora", SearchFold.Strict("Қора"));
        Assert.Equal("kora", SearchFold.Strict("Кора"));
        Assert.Equal("g'isht", SearchFold.Strict("Ғишт"));
        Assert.Equal("o'rik", SearchFold.Strict("Ўрик"));
    }

    [Fact]
    public void WP10_Sh_and_s_remain_distinct()
    {
        Assert.NotEqual(SearchFold.Fuzzy("shar"), SearchFold.Fuzzy("sar"));
    }

    [Theory]
    [InlineData("G'isht")]
    [InlineData("Gʻisht")]
    [InlineData("Gʼisht")]
    [InlineData("G‘isht")]
    [InlineData("G’isht")]
    [InlineData("G´isht")]
    public void WP10_Apostrophe_variants_are_normalized(string value)
    {
        Assert.Equal("g'isht", SearchFold.Strict(value));
    }

    [Fact]
    public void WP10_Empty_and_null_inputs_are_safe()
    {
        Assert.Equal(string.Empty, SearchFold.Strict(null));
        Assert.Equal(string.Empty, SearchFold.Fuzzy(null));
        Assert.Equal(string.Empty, SearchFold.Strict("  \t\r\n "));
        Assert.Equal(string.Empty, SearchFold.Fuzzy(string.Empty));
    }
}
