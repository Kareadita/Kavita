using HtmlAgilityPack;
using Kavita.Services.Kobo;
using Xunit;

namespace Kavita.Services.Tests.Kobo;

public class KoboLocationMapperTests
{
    [Fact]
    public void MapHtmlToBookScrollId_ReturnsIdXPath_WhenElementExists()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("""
            <html><body>
              <p>intro</p>
              <span id="kobo.1.2">exact</span>
            </body></html>
            """);

        Assert.Equal("id(\"kobo.1.2\")", KoboLocationMapper.MapHtmlToBookScrollId(doc, "kobo.1.2"));
    }

    [Fact]
    public void MapHtmlToBookScrollId_ReturnsNull_WhenElementMissing()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("<html><body><p id=\"other\">x</p></body></html>");

        Assert.Null(KoboLocationMapper.MapHtmlToBookScrollId(doc, "kobo.9.9"));
    }

    [Fact]
    public void MapHtmlToLocation_ReturnsKoboSpan_WhenAncestorHasKoboId()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("""
            <html><body>
              <span id="kobo.3.1"><p>inner text</p></span>
            </body></html>
            """);

        var mapped = KoboLocationMapper.MapHtmlToLocation(doc, "//body/span/p", "OEBPS/ch.xhtml");
        Assert.NotNull(mapped);
        Assert.Equal("kobo.3.1", mapped.Value);
        Assert.Equal(KoboLocationMapper.TypeKoboSpan, mapped.Type);
        Assert.Equal("OEBPS/ch.xhtml", mapped.Source);
    }

    [Fact]
    public void MapHtmlToLocation_ReturnsNull_WhenNoKoboSpanInTree()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("""
            <html><body>
              <p id="chapter-intro">plain epub</p>
            </body></html>
            """);

        // Must not invent a KoboSpan from a non-kobo id.
        Assert.Null(KoboLocationMapper.MapHtmlToLocation(doc, "id(\"chapter-intro\")", "OEBPS/ch.xhtml"));
    }

    [Fact]
    public void MapHtmlToLocation_UsesIdShortcut()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("""
            <html><body>
              <span id="kobo.4.2">here</span>
            </body></html>
            """);

        var mapped = KoboLocationMapper.MapHtmlToLocation(doc, "id(\"kobo.4.2\")", "text/part.xhtml");
        Assert.NotNull(mapped);
        Assert.Equal("kobo.4.2", mapped.Value);
        Assert.Equal("text/part.xhtml", mapped.Source);
    }
}
