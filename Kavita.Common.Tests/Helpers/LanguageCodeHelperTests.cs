using Kavita.Common.Helpers;

namespace Kavita.Common.Tests.Helpers;

public class LanguageCodeHelperTests
{
    [Theory]
    [InlineData("en", true)]
    [InlineData("ja-Latn", true)]
    [InlineData("pt-BR", true)]
    [InlineData("en_US", false)]
    [InlineData("ja-Latin", false)]
    [InlineData("", false)]
    public void IsWellFormed_ChecksShapeOnly(string code, bool expected)
    {
        Assert.Equal(expected, LanguageCodeHelper.IsWellFormed(code));
    }

    [Theory]
    [InlineData("{Native}", true)]
    [InlineData("{native}", true)]
    [InlineData("{NATIVE}", true)]
    [InlineData("{Romaji}", true)]
    [InlineData("{romaji}", true)]
    [InlineData("en", false)]
    [InlineData("Native", false)]
    [InlineData("", false)]
    public void IsReservedToken_MatchesTokensCaseInsensitively(string code, bool expected)
    {
        Assert.Equal(expected, LanguageCodeHelper.IsReservedToken(code));
    }

    [Theory]
    [InlineData("{Native}", true, false)]
    [InlineData("{romaji}", false, true)]
    [InlineData("en", false, false)]
    public void IsNativeToken_IsRomajiToken_DiscriminateTokens(string code, bool isNative, bool isRomaji)
    {
        Assert.Equal(isNative, LanguageCodeHelper.IsNativeToken(code));
        Assert.Equal(isRomaji, LanguageCodeHelper.IsRomajiToken(code));
    }

    [Fact]
    public void Sanitize_KeepsReservedTokensAndWellFormedCodes_DropsMalformed()
    {
        // {Native}/{Romaji} are not valid BCP-47 but are reserved tokens, so they survive; en_US is malformed and dropped
        Assert.Equal("en;{Native};{Romaji};ja-Latn", LanguageCodeHelper.Sanitize("en;{Native};{Romaji};en_US;ja-Latn"));
    }
}
