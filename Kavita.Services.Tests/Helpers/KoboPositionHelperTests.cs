using HtmlAgilityPack;
using Kavita.Services.Helpers.Kobo;

namespace Kavita.Services.Tests.Helpers;

public class KoboPositionHelperTests
{
    /// <summary>
    /// From Spice and Wolf, Vol. 1
    /// </summary>
    private const string KoboHtml = """
                                    <html><body>
                                    <p><span class="koboSpan" id="kobo.1.1">Lawrence mulled it over. </span><span class="koboSpan" id="kobo.1.2">Holo seemed confident that she could sell the marten pelts higher than he could. </span><span class="koboSpan" id="kobo.1.3">Wisewolf or no, he had seven years of experience as a traveling merchant. </span><span class="koboSpan" id="kobo.1.4">He wasn’t such a weak dealer that a few words tossed in from the side would bring up his prices, and there was no guarantee the buyer would accept such prices.</span></p>
                                    <p id="p-2"><span class="koboSpan" id="kobo.2.1">Yet his curiosity at exactly how she would attempt this farce overpowered his doubt that it would actually happen, so in the end he said, “Agreed.”</span></p>
                                    <p><span class="koboSpan" id="kobo.3.1">“It’s done, then!” </span><span class="koboSpan" id="kobo.3.2">replied Holo, burping.</span></p>
                                    <p><span class="koboSpan" id="kobo.4.1">“But this isn’t just limited to our pelts. </span><span class="koboSpan" id="kobo.4.2">You’re a merchant, too—there may be no chance for me to talk up our price.”</span></p>
                                    <p><span class="koboSpan" id="kobo.5.1">“How modest of you.”</span></p>
                                    <p><span class="koboSpan" id="kobo.6.1">“Wisdom is knowing thyself first.”</span></p>
                                    <p><span class="koboSpan" id="kobo.7.1">The statement would’ve sounded better had she not said it while casting her gaze longingly back toward the remaining pile of apples.</span></p>
                                    </body></html>
                                    """;

    public static HtmlDocument Load(string html = KoboHtml)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc;
    }

    [Fact]
    public void FindNonKoboParent_NoId()
    {
        var doc = Load();
        var bookId = KoboPositionHelper.Decode(doc, doc, "kobo.6.1");

        Assert.Equal("//body/p[6]", bookId);
    }

    [Fact]
    public void FindNonKoboParent_Id()
    {
        var doc = Load();
        var bookId = KoboPositionHelper.Decode(doc, doc, "kobo.2.1");

        Assert.Equal("id(\"p-2\")", bookId);
    }

    [Fact]
    public void Decode_StripsKepubWrappersAgainstOriginal()
    {
        var kepubDoc = Load(KepubChapter);
        var readerDoc = Load(OriginalChapter);

        var bookId = KoboPositionHelper.Decode(kepubDoc, readerDoc, "kobo.4.1");

        Assert.Equal("//body/div[1]/section[1]/p[3]", bookId);
    }

    [Fact]
    public void Decode_ClimbsPastInlineAncestors()
    {
        var kepubDoc = Load(KepubChapter);
        var readerDoc = Load(OriginalChapter);

        var bookId = KoboPositionHelper.Decode(kepubDoc, readerDoc, "kobo.4.2");

        Assert.Equal("//body/div[1]/section[1]/p[3]", bookId);
    }

    [Fact]
    public void Decode_KeepsWrappersWhenReaderAlsoHasThem()
    {
        var kepubDoc = Load(KepubChapter);

        var bookId = KoboPositionHelper.Decode(kepubDoc, kepubDoc, "kobo.4.1");

        Assert.Equal("//body/div[1]/div[1]/div[1]/section[1]/p[3]", bookId);
    }

    [Fact]
    public void Decode_WithoutReaderDocument_ReturnsTranslatedPath()
    {
        var kepubDoc = Load(KepubChapter);

        var bookId = KoboPositionHelper.Decode(kepubDoc, null, "kobo.4.1");

        Assert.Equal("//body/div[1]/section[1]/p[3]", bookId);
    }

    [Fact]
    public void Decode_UsesIdPresentInReaderDocument()
    {
        var kepubDoc = Load(KepubChapter);
        var readerDoc = Load(OriginalChapter);

        var bookId = KoboPositionHelper.Decode(kepubDoc, readerDoc, "kobo.3.1");

        Assert.Equal("id(\"p-2\")", bookId);
    }

    [Fact]
    public void Decode_FallsBackToAncestorId()
    {
        const string minimalReader = """<html><body><section id="chapter015"></section></body></html>""";
        var kepubDoc = Load(KepubChapter);
        var readerDoc = Load(minimalReader);

        var bookId = KoboPositionHelper.Decode(kepubDoc, readerDoc, "kobo.4.1");

        Assert.Equal("id(\"chapter015\")", bookId);
    }

    [Fact]
    public void Encode_FindsSpanByPositionalPath()
    {
        var kepubDoc = Load(KepubChapter);
        var readerDoc = Load(OriginalChapter);

        var koboSpan = KoboPositionHelper.Encode(readerDoc, kepubDoc, "//body/div[1]/section[1]/p[3]");

        Assert.Equal("kobo.4.1", koboSpan);
    }

    [Fact]
    public void Encode_FindsSpanById()
    {
        var kepubDoc = Load(KepubChapter);
        var readerDoc = Load(OriginalChapter);

        var koboSpan = KoboPositionHelper.Encode(readerDoc, kepubDoc, "id(\"p-2\")");

        Assert.Equal("kobo.3.1", koboSpan);
    }

    [Fact]
    public void Encode_InlineElement_ReturnsSpanInsideIt()
    {
        var kepubDoc = Load(KepubChapter);
        var readerDoc = Load(OriginalChapter);

        var koboSpan = KoboPositionHelper.Encode(readerDoc, kepubDoc, "//body/div[1]/section[1]/p[3]/em[1]");

        Assert.Equal("kobo.4.2", koboSpan);
    }

    [Theory]
    [InlineData("//body/div[1]/section[1]/p[3]")]
    [InlineData("id(\"p-2\")")]
    public void Encode_ThenDecode_RoundTrips(string xPath)
    {
        var kepubDoc = Load(KepubChapter);
        var readerDoc = Load(OriginalChapter);

        var koboSpan = KoboPositionHelper.Encode(readerDoc, kepubDoc, xPath);
        Assert.NotEmpty(koboSpan);

        var decoded = KoboPositionHelper.Decode(kepubDoc, readerDoc, koboSpan);

        Assert.Equal(xPath, decoded);
    }

    private const string OriginalChapter = """
                                           <html><body><div class="galley-rw"><section id="chapter015">
                                           <h1>Heading</h1>
                                           <p>First paragraph.</p>
                                           <p id="p-2">Second paragraph.</p>
                                           <p>Third <em>emphasised</em> paragraph. He didn&rsquo;t stop.</p>
                                           </section></div></body></html>
                                           """;

    private const string KepubChapter = """
        <html><body><div id="book-columns"><div id="book-inner"><div class="galley-rw"><section id="chapter015">
        <h1><span class="koboSpan" id="kobo.1.1">Heading</span></h1>
        <p><span class="koboSpan" id="kobo.2.1">First paragraph.</span></p>
        <p id="p-2"><span class="koboSpan" id="kobo.3.1">Second paragraph.</span></p>
        <p><span class="koboSpan" id="kobo.4.1">Third </span><em><span class="koboSpan" id="kobo.4.2">emphasised</span></em><span class="koboSpan" id="kobo.4.3"> paragraph. He didn’t stop.</span></p>
        </section></div></div></div></body></html>
        """;
}
