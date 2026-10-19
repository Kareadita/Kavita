using HtmlAgilityPack;
using Kavita.Services.Helpers;

namespace Kavita.Services.Tests.Helpers;

public class AnnotationHelperKoboTests
{
    /// <summary>
    /// From Spice and Wolf, Vol. 1
    /// </summary>
    private const string KoboHtml = """
                                    <html><body>
                                    <p><span class="koboSpan" id="kobo.1.1">Lawrence mulled it over. </span><span class="koboSpan" id="kobo.1.2">Holo seemed confident that she could sell the marten pelts higher than he could. </span><span class="koboSpan" id="kobo.1.3">Wisewolf or no, he had seven years of experience as a traveling merchant. </span><span class="koboSpan" id="kobo.1.4">He wasn’t such a weak dealer that a few words tossed in from the side would bring up his prices, and there was no guarantee the buyer would accept such prices.</span></p>
                                    <p><span class="koboSpan" id="kobo.2.1">Yet his curiosity at exactly how she would attempt this farce overpowered his doubt that it would actually happen, so in the end he said, “Agreed.”</span></p>
                                    <p><span class="koboSpan" id="kobo.3.1">“It’s done, then!” </span><span class="koboSpan" id="kobo.3.2">replied Holo, burping.</span></p>
                                    <p><span class="koboSpan" id="kobo.4.1">“But this isn’t just limited to our pelts. </span><span class="koboSpan" id="kobo.4.2">You’re a merchant, too—there may be no chance for me to talk up our price.”</span></p>
                                    <p><span class="koboSpan" id="kobo.5.1">“How modest of you.”</span></p>
                                    <p><span class="koboSpan" id="kobo.6.1">“Wisdom is knowing thyself first.”</span></p>
                                    <p><span class="koboSpan" id="kobo.7.1">The statement would’ve sounded better had she not said it while casting her gaze longingly back toward the remaining pile of apples.</span></p>
                                    </body></html>
                                    """;

    private static HtmlDocument Load(string html = KoboHtml)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc;
    }

    [Fact]
    public void Test_TryResolveAnchors_AcrossTwoParagraphs()
    {
        var doc = Load();

        const string selected = "replied Holo, burping.\n\n“But this isn’t just limited to our pelts. You’re a merchant, too—there may be no chance for me to talk";

        var result = AnnotationHelper.TryResolveAnchors(doc, "//body/p[3]", "//body/p[4]", selected, out var start, out var end);

        Assert.True(result);

        // "“It’s done, then!” " is all kobo.3.1, so the start lands at the very beginning of kobo.3.2
        Assert.Equal("span#kobo.3.2", start.Path);
        Assert.Equal(0, start.Offset);

        Assert.Equal("span#kobo.4.2", end.Path);
        Assert.Equal("You’re a merchant, too—there may be no chance for me to talk".Length, end.Offset);
    }

    [Fact]
    public void Test_TryResolveAnchors_SingleSpan()
    {
        var doc = Load();

        var result = AnnotationHelper.TryResolveAnchors(doc, "//body/p[1]", "//body/p[1]", "Holo seemed confident", out var start, out var end);

        Assert.True(result);
        Assert.Equal("span#kobo.1.2", start.Path);
        Assert.Equal(0, start.Offset);
        Assert.Equal("span#kobo.1.2", end.Path);
        Assert.Equal("Holo seemed confident".Length, end.Offset);
    }

    [Fact]
    public void Test_TryResolveAnchors_SingleParagraphAcrossSpans()
    {
        var doc = Load();

        var result = AnnotationHelper.TryResolveAnchors(doc, "//body/p[1]", "//body/p[1]", "mulled it over. Holo seemed", out var start, out var end);

        Assert.True(result);
        Assert.Equal("span#kobo.1.1", start.Path);
        Assert.Equal("Lawrence ".Length, start.Offset);
        Assert.Equal("span#kobo.1.2", end.Path);
        Assert.Equal("Holo seemed".Length, end.Offset);
    }

    [Fact]
    public void Test_TryResolveAnchors_StartsMidSpan_EndsMidSpan()
    {
        var doc = Load();

        var result = AnnotationHelper.TryResolveAnchors(doc, "//body/p[5]", "//body/p[6]", "modest of you.”\n\n“Wisdom is knowing", out var start, out var end);

        Assert.True(result);
        Assert.Equal("span#kobo.5.1", start.Path);
        Assert.Equal("“How ".Length, start.Offset);
        Assert.Equal("span#kobo.6.1", end.Path);
        Assert.Equal("“Wisdom is knowing".Length, end.Offset);
    }

    [Fact]
    public void Test_TryResolveAnchors_WhitespaceInSelectionIsNormalized()
    {
        var doc = Load();

        // Browsers collapse the paragraph break to a single space (or a newline); both must match
        var result = AnnotationHelper.TryResolveAnchors(doc, "//body/p[5]", "//body/p[6]", "you.” “Wisdom", out var start, out var end);

        Assert.True(result);
        Assert.Equal("span#kobo.5.1", start.Path);
        Assert.Equal("span#kobo.6.1", end.Path);
        Assert.Equal("“Wisdom".Length, end.Offset);
    }

    [Fact]
    public void Test_TryResolveAnchors_OffsetCountsDecodedCharacterReferences()
    {
        var doc = Load("<html><body><p><span id=\"kobo.1.1\">Spice &amp; Wolf</span></p></body></html>");

        var result = AnnotationHelper.TryResolveAnchors(doc, "//body/p[1]", "//body/p[1]", "Wolf", out var start, out var end);

        Assert.True(result);
        Assert.Equal("span#kobo.1.1", start.Path);
        // "&amp;" is a single character, so "Spice & " is 6 characters, not 10
        Assert.Equal("Spice & ".Length, start.Offset);
        Assert.Equal("Spice & Wolf".Length, end.Offset);
    }

    [Fact]
    public void Test_TryResolveAnchors_NoIdFallsBackToXPath()
    {
        var doc = Load("<html><body><p>Hello</p><p>World</p></body></html>");

        var result = AnnotationHelper.TryResolveAnchors(doc, "//body/p[1]", "//body/p[2]", "lo\n\nWor", out var start, out var end);

        Assert.True(result);
        Assert.Equal("/html[1]/body[1]/p[1]", start.Path);
        Assert.Equal(3, start.Offset);
        Assert.Equal("/html[1]/body[1]/p[2]", end.Path);
        Assert.Equal(3, end.Offset);
    }

    [Fact]
    public void Test_TryResolveAnchors_TextNotFound_ReturnsFalse()
    {
        var doc = Load();

        var result = AnnotationHelper.TryResolveAnchors(doc, "//body/p[3]", "//body/p[4]", "this text is not in the book", out var start, out var end);

        Assert.False(result);
        Assert.Equal(default, start);
        Assert.Equal(default, end);
    }

    [Fact]
    public void Test_TryResolveAnchors_ElementNotFound_ReturnsFalse()
    {
        var doc = Load();

        var result = AnnotationHelper.TryResolveAnchors(doc, "//body/p[99]", "//body/p[4]", "replied Holo", out _, out _);

        Assert.False(result);
    }
}
