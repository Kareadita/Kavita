using Kavita.Services.Helpers.Kobo;

namespace Kavita.Services.Tests.Helpers;

public class KoboPositionHelperTests
{
    [Fact]
    public void FindNonKoboParent_NoId()
    {
        var doc = AnnotationHelperKoboTests.Load();
        var bookId = KoboPositionHelper.Decode(doc, doc, "kobo.6.1");

        Assert.Equal("//body/p[6]", bookId);
    }

    [Fact]
    public void FindNonKoboParent_Id()
    {
        var doc = AnnotationHelperKoboTests.Load();
        var bookId = KoboPositionHelper.Decode(doc, doc, "kobo.2.1");

        Assert.Equal("id(\"p-2\")", bookId);
    }

    [Fact]
    public void Decode_StripsKepubWrappersAgainstOriginal()
    {
        var kepubDoc = AnnotationHelperKoboTests.Load(KepubChapter);
        var readerDoc = AnnotationHelperKoboTests.Load(OriginalChapter);

        var bookId = KoboPositionHelper.Decode(kepubDoc, readerDoc, "kobo.4.1");

        Assert.Equal("//body/div[1]/section[1]/p[3]", bookId);
    }

    [Fact]
    public void Decode_ClimbsPastInlineAncestors()
    {
        var kepubDoc = AnnotationHelperKoboTests.Load(KepubChapter);
        var readerDoc = AnnotationHelperKoboTests.Load(OriginalChapter);

        var bookId = KoboPositionHelper.Decode(kepubDoc, readerDoc, "kobo.4.2");

        Assert.Equal("//body/div[1]/section[1]/p[3]", bookId);
    }

    [Fact]
    public void Decode_KeepsWrappersWhenReaderAlsoHasThem()
    {
        var kepubDoc = AnnotationHelperKoboTests.Load(KepubChapter);

        var bookId = KoboPositionHelper.Decode(kepubDoc, kepubDoc, "kobo.4.1");

        Assert.Equal("//body/div[1]/div[1]/div[1]/section[1]/p[3]", bookId);
    }

    [Fact]
    public void Decode_WithoutReaderDocument_ReturnsTranslatedPath()
    {
        var kepubDoc = AnnotationHelperKoboTests.Load(KepubChapter);

        var bookId = KoboPositionHelper.Decode(kepubDoc, null, "kobo.4.1");

        Assert.Equal("//body/div[1]/section[1]/p[3]", bookId);
    }

    [Fact]
    public void Decode_UsesIdPresentInReaderDocument()
    {
        var kepubDoc = AnnotationHelperKoboTests.Load(KepubChapter);
        var readerDoc = AnnotationHelperKoboTests.Load(OriginalChapter);

        var bookId = KoboPositionHelper.Decode(kepubDoc, readerDoc, "kobo.3.1");

        Assert.Equal("id(\"p-2\")", bookId);
    }

    [Fact]
    public void Decode_FallsBackToAncestorId()
    {
        const string minimalReader = """<html><body><section id="chapter015"></section></body></html>""";
        var kepubDoc = AnnotationHelperKoboTests.Load(KepubChapter);
        var readerDoc = AnnotationHelperKoboTests.Load(minimalReader);

        var bookId = KoboPositionHelper.Decode(kepubDoc, readerDoc, "kobo.4.1");

        Assert.Equal("id(\"chapter015\")", bookId);
    }

    [Fact]
    public void Encode_FindsSpanByPositionalPath()
    {
        var kepubDoc = AnnotationHelperKoboTests.Load(KepubChapter);
        var readerDoc = AnnotationHelperKoboTests.Load(OriginalChapter);

        var koboSpan = KoboPositionHelper.Encode(readerDoc, kepubDoc, "//body/div[1]/section[1]/p[3]");

        Assert.Equal("kobo.4.1", koboSpan);
    }

    [Fact]
    public void Encode_FindsSpanById()
    {
        var kepubDoc = AnnotationHelperKoboTests.Load(KepubChapter);
        var readerDoc = AnnotationHelperKoboTests.Load(OriginalChapter);

        var koboSpan = KoboPositionHelper.Encode(readerDoc, kepubDoc, "id(\"p-2\")");

        Assert.Equal("kobo.3.1", koboSpan);
    }

    [Fact]
    public void Encode_InlineElement_ReturnsSpanInsideIt()
    {
        var kepubDoc = AnnotationHelperKoboTests.Load(KepubChapter);
        var readerDoc = AnnotationHelperKoboTests.Load(OriginalChapter);

        var koboSpan = KoboPositionHelper.Encode(readerDoc, kepubDoc, "//body/div[1]/section[1]/p[3]/em[1]");

        Assert.Equal("kobo.4.2", koboSpan);
    }

    [Theory]
    [InlineData("//body/div[1]/section[1]/p[3]")]
    [InlineData("id(\"p-2\")")]
    public void Encode_ThenDecode_RoundTrips(string xPath)
    {
        var kepubDoc = AnnotationHelperKoboTests.Load(KepubChapter);
        var readerDoc = AnnotationHelperKoboTests.Load(OriginalChapter);

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
