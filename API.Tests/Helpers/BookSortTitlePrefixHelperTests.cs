using API.Helpers;
using Xunit;

namespace API.Tests.Helpers;

public class BookSortTitlePrefixHelperTests
{
    [Theory]
    [InlineData("The Avengers", "Avengers")]
    [InlineData("A Game of Thrones", "Game of Thrones")]
    [InlineData("An American Tragedy", "American Tragedy")]
    public void TestEnglishPrefixes(string inputString, string expected)
    {
        Assert.Equal(expected, BookSortTitlePrefixHelper.GetSortTitle(inputString));
    }

    [Theory]
    [InlineData("El Quijote", "Quijote")]
    [InlineData("La Casa de Papel", "Casa de Papel")]
    [InlineData("Los Miserables", "Miserables")]
    [InlineData("Las Vegas", "Vegas")]
    [InlineData("Un Mundo Feliz", "Mundo Feliz")]
    [InlineData("Una Historia", "Historia")]
    public void TestSpanishPrefixes(string inputString, string expected)
    {
        Assert.Equal(expected, BookSortTitlePrefixHelper.GetSortTitle(inputString));
    }

    [Theory]
    [InlineData("Le Petit Prince", "Petit Prince")]
    [InlineData("La Belle et la Bête", "Belle et la Bête")]
    [InlineData("Les Misérables", "Misérables")]
    [InlineData("Un Amour de Swann", "Amour de Swann")]
    [InlineData("Une Vie", "Vie")]
    [InlineData("Des Souris et des Hommes", "Souris et des Hommes")]
    public void TestFrenchPrefixes(string inputString, string expected)
    {
        Assert.Equal(expected, BookSortTitlePrefixHelper.GetSortTitle(inputString));
    }

    [Theory]
    [InlineData("Der Herr der Ringe", "Herr der Ringe")]
    [InlineData("Die Verwandlung", "Verwandlung")]
    [InlineData("Das Kapital", "Kapital")]
    [InlineData("Ein Sommernachtstraum", "Sommernachtstraum")]
    [InlineData("Eine Geschichte", "Geschichte")]
    public void TestGermanPrefixes(string inputString, string expected)
    {
        Assert.Equal(expected, BookSortTitlePrefixHelper.GetSortTitle(inputString));
    }

    [Theory]
    [InlineData("Il Nome della Rosa", "Nome della Rosa")]
    [InlineData("La Divina Commedia", "Divina Commedia")]
    [InlineData("Lo Hobbit", "Hobbit")]
    [InlineData("Gli Ultimi", "Ultimi")]
    [InlineData("Le Città Invisibili", "Città Invisibili")]
    [InlineData("Un Giorno", "Giorno")]
    [InlineData("Una Notte", "Notte")]
    public void TestItalianPrefixes(string inputString, string expected)
    {
        Assert.Equal(expected, BookSortTitlePrefixHelper.GetSortTitle(inputString));
    }

    [Theory]
    [InlineData("O Alquimista", "Alquimista")]
    [InlineData("A Moreninha", "Moreninha")]
    [InlineData("Os Lusíadas", "Lusíadas")]
    [InlineData("As Meninas", "Meninas")]
    [InlineData("Um Defeito de Cor", "Defeito de Cor")]
    [InlineData("Uma História", "História")]
    public void TestPortuguesePrefixes(string inputString, string expected)
    {
        Assert.Equal(expected, BookSortTitlePrefixHelper.GetSortTitle(inputString));
    }

    [Theory]
    [InlineData("", "")] // Empty string returns empty
    [InlineData("Book", "Book")] // Single word, no change
    [InlineData("Avengers", "Avengers")] // No prefix, no change
    public void TestNoPrefixCases(string inputString, string expected)
    {
        Assert.Equal(expected, BookSortTitlePrefixHelper.GetSortTitle(inputString));
    }

    [Theory]
    [InlineData("The", "The")] // Just a prefix word alone
    [InlineData("A", "A")] // Just single letter prefix alone
    [InlineData("Le", "Le")] // French prefix alone
    public void TestPrefixWordAlone(string inputString, string expected)
    {
        Assert.Equal(expected, BookSortTitlePrefixHelper.GetSortTitle(inputString));
    }

    [Theory]
    [InlineData("THE AVENGERS", "AVENGERS")] // All caps
    [InlineData("the avengers", "avengers")] // All lowercase
    [InlineData("The AVENGERS", "AVENGERS")] // Mixed case
    [InlineData("tHe AvEnGeRs", "AvEnGeRs")] // Random case
    public void TestCaseInsensitivity(string inputString, string expected)
    {
        Assert.Equal(expected, BookSortTitlePrefixHelper.GetSortTitle(inputString));
    }

    [Theory]
    [InlineData("Then Came You", "Then Came You")] // "The" + "n" = not a prefix
    [InlineData("And Then There Were None", "And Then There Were None")] // "An" + "d" = not a prefix
    [InlineData("Elsewhere", "Elsewhere")] // "El" + "sewhere" = not a prefix (no space)
    [InlineData("Lesson Plans", "Lesson Plans")] // "Les" + "son" = not a prefix (no space)
    [InlineData("Theory of Everything", "Theory of Everything")] // "The" + "ory" = not a prefix
    public void TestFalsePositivePrefixes(string inputString, string expected)
    {
        Assert.Equal(expected, BookSortTitlePrefixHelper.GetSortTitle(inputString));
    }

    [Theory]
    [InlineData("The ", "The ")] // Prefix with only space after - returns original
    [InlineData("La ", "La ")] // Same for other languages
    [InlineData("El ", "El ")] // Same for Spanish
    public void TestPrefixWithOnlySpaceAfter(string inputString, string expected)
    {
        Assert.Equal(expected, BookSortTitlePrefixHelper.GetSortTitle(inputString));
    }

    [Theory]
    [InlineData("The  Multiple   Spaces", " Multiple   Spaces")] // Doesn't trim extra spaces from remainder
    [InlineData("Le  Petit Prince", " Petit Prince")] // Leading space preserved in remainder
    public void TestSpaceHandling(string inputString, string expected)
    {
        Assert.Equal(expected, BookSortTitlePrefixHelper.GetSortTitle(inputString));
    }

    [Theory]
    [InlineData("The The Matrix", "The Matrix")] // Removes first "The", leaves second
    [InlineData("A A Clockwork Orange", "A Clockwork Orange")] // Removes first "A", leaves second
    [InlineData("El El Cid", "El Cid")] // Spanish version
    public void TestRepeatedPrefixes(string inputString, string expected)
    {
        Assert.Equal(expected, BookSortTitlePrefixHelper.GetSortTitle(inputString));
    }

    [Theory]
    [InlineData("L'Étranger", "L'Étranger")] // French contraction - no space, no change
    [InlineData("D'Artagnan", "D'Artagnan")] // Contraction - no space, no change
    [InlineData("The-Matrix", "The-Matrix")] // Hyphen instead of space - no change
    [InlineData("The.Avengers", "The.Avengers")] // Period instead of space - no change
    public void TestNonSpaceSeparators(string inputString, string expected)
    {
        Assert.Equal(expected, BookSortTitlePrefixHelper.GetSortTitle(inputString));
    }

    [Theory]
    [InlineData("三国演义", "三国演义")] // Chinese - no processing due to CJK detection
    [InlineData("한국어", "한국어")] // Korean - not in CJK range, would be processed normally
    public void TestCjkLanguages(string inputString, string expected)
    {
        // NOTE: These don't do anything, I am waiting for user input on if these are needed
        Assert.Equal(expected, BookSortTitlePrefixHelper.GetSortTitle(inputString));
    }

    [Theory]
    [InlineData("नमस्ते दुनिया", "नमस्ते दुनिया")] // Hindi - not CJK, processed normally
    [InlineData("مرحبا بالعالم", "مرحبا بالعالم")] // Arabic - not CJK, processed normally
    [InlineData("שלום עולם", "שלום עולם")] // Hebrew - not CJK, processed normally
    public void TestNonLatinNonCjkScripts(string inputString, string expected)
    {
        Assert.Equal(expected, BookSortTitlePrefixHelper.GetSortTitle(inputString));
    }

    [Theory]
    [InlineData("в мире", "мире")] // Russian "в" (in) - should be removed
    [InlineData("на столе", "столе")] // Russian "на" (on) - should be removed
    [InlineData("с друзьями", "друзьями")] // Russian "с" (with) - should be removed
    public void TestRussianPrefixes(string inputString, string expected)
    {
        Assert.Equal(expected, BookSortTitlePrefixHelper.GetSortTitle(inputString));
    }
}
