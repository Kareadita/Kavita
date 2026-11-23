using Kavita.Common.Helpers;

namespace Kavita.Common.Tests.Helpers;

public class HtmlHelperTests
{
    #region GetCharacters Tests

    [Fact]
    public void GetCharacters_WithNullBody_ReturnsNull()
    {

        string body = null;

        // Act
        var result = HtmlHelper.GetCharacters(body);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetCharacters_WithEmptyBody_ReturnsEmptyString()
    {

        var body = string.Empty;

        // Act
        var result = HtmlHelper.GetCharacters(body);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetCharacters_WithNoTextNodes_ReturnsEmptyString()
    {

        const string body = "<div></div>";

        // Act
        var result = HtmlHelper.GetCharacters(body);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetCharacters_WithLessCharactersThanLimit_ReturnsFullText()
    {

        var body = "<p>This is a short review.</p>";

        // Act
        var result = HtmlHelper.GetCharacters(body);

        // Assert
        Assert.Equal("This is a short review.…", result);
    }

    [Fact]
    public void GetCharacters_WithMoreCharactersThanLimit_TruncatesText()
    {

        var body = "<p>" + new string('a', 200) + "</p>";

        // Act
        var result = HtmlHelper.GetCharacters(body);

        // Assert
        Assert.Equal(new string('a', 175) + "…", result);
        Assert.Equal(176, result.Length); // 175 characters + ellipsis
    }

    [Fact]
    public void GetCharacters_IgnoresScriptTags()
    {

        const string body = "<p>Visible text</p><script>console.log('hidden');</script>";

        // Act
        var result = HtmlHelper.GetCharacters(body);

        // Assert
        Assert.Equal("Visible text…", result);
        Assert.DoesNotContain("hidden", result);
    }

    [Fact]
    public void GetCharacters_RemovesMarkdownSymbols()
    {

        const string body = "<p>This is **bold** and _italic_ text with [link](url).</p>";

        // Act
        var result = HtmlHelper.GetCharacters(body);

        // Assert
        Assert.Equal("This is bold and italic text with link.…", result);
    }

    [Fact]
    public void GetCharacters_HandlesComplexMarkdownAndHtml()
    {

        const string body = """

                                        <div>
                                            <h1># Header</h1>
                                            <p>This is ~~strikethrough~~ and __underlined__ text</p>
                                            <p>~~~code block~~~</p>
                                            <p>+++highlighted+++</p>
                                            <p>img123(image.jpg)</p>
                                        </div>
                            """;

        // Act
        var result = HtmlHelper.GetCharacters(body);

        // Assert
        Assert.DoesNotContain("~~", result);
        Assert.DoesNotContain("__", result);
        Assert.DoesNotContain("~~~", result);
        Assert.DoesNotContain("+++", result);
        Assert.DoesNotContain("img123(", result);
        Assert.Contains("Header", result);
        Assert.Contains("strikethrough", result);
        Assert.Contains("underlined", result);
        Assert.Contains("code block", result);
        Assert.Contains("highlighted", result);
    }

    #endregion

    #region ToPlainText Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<p></p>")]
    [InlineData("<div><br/></div>")]
    [InlineData("<script>alert(1)</script>")]
    public void ToPlainText_NoText_ReturnsEmpty(string? html)
    {
        Assert.Equal(string.Empty, HtmlHelper.ToPlainText(html));
    }

    [Theory]
    [InlineData("Plain text", "Plain text")]
    [InlineData("<p>First.</p><p>Second.</p>", "First. Second.")]
    [InlineData("<h1>Title</h1>Body", "Title Body")]
    [InlineData("Line one<br>Line two", "Line one Line two")]
    [InlineData("<ul><li>One</li><li>Two</li></ul>", "One Two")]
    [InlineData("<table><tr><td>A</td><td>B</td></tr></table>", "A B")]
    public void ToPlainText_BlockElements_SeparateWords(string html, string expected)
    {
        Assert.Equal(expected, HtmlHelper.ToPlainText(html));
    }

    [Theory]
    [InlineData("<b>Tom</b>'s", "Tom's")]
    [InlineData("Spi<i>ce</i> and <em>Wolf</em>", "Spice and Wolf")]
    [InlineData("<p><span>Holo</span>, <a href=\"#\">the wise</a> wolf</p>", "Holo, the wise wolf")]
    public void ToPlainText_InlineElements_KeepWordsTogether(string html, string expected)
    {
        Assert.Equal(expected, HtmlHelper.ToPlainText(html));
    }

    [Theory]
    [InlineData("Tom &amp; Jerry", "Tom & Jerry")]
    [InlineData("5 &lt; 6", "5 < 6")]
    [InlineData("a&nbsp;&nbsp;b", "a b")]
    [InlineData("<p>\r\n    Indented\n    text\n</p>", "Indented text")]
    public void ToPlainText_DecodesEntitiesAndCollapsesWhitespace(string html, string expected)
    {
        Assert.Equal(expected, HtmlHelper.ToPlainText(html));
    }

    [Theory]
    [InlineData("<style>p{color:red}</style><p>Hi</p>", "Hi")]
    [InlineData("<p>Hi<!-- note --></p>", "Hi")]
    [InlineData("<p>Unclosed <b>bold", "Unclosed bold")]
    public void ToPlainText_SkipsNonTextAndToleratesBrokenMarkup(string html, string expected)
    {
        Assert.Equal(expected, HtmlHelper.ToPlainText(html));
    }

    #endregion
}
