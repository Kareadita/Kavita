using HtmlAgilityPack;
using Kavita.Models.DTOs.Reader;
using Kavita.Services.Helpers;

namespace Kavita.Services.Tests.Helpers;

public class AnnotationHelperTests
{

    [Fact]
    public void Test_InjectSingleElementAnnotations_TrailingWhiteSpace()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("<html><body><p id='para1'>　意識が芽生えてから二日が経過した。</p></body></html>");

        var annotation = new AnnotationDto
        {
            XPath = """id("para1")""",
            EndingXPath = """id("para1")""",
            SelectedText = "意識が芽生",
            ChapterId = 0,
            VolumeId = 0,
            SeriesId = 0,
            LibraryId = 0,
            OwnerUserId = 0,
        };

        AnnotationHelper.InjectSingleElementAnnotations(doc, [annotation]);
        Assert.Equal(
            """　<app-epub-highlight id="epub-highlight-0" data-annotation-id="0">意識が芽生</app-epub-highlight>えてから二日が経過した。""",
            doc.GetElementbyId("para1").InnerHtml
        );
    }

    [Fact]
    public void Test_InjectSingleElementAnnotations_WhitespacePositions()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("<html><body><p id='para1'>Spice and    Wolf is       Amazing!</p></body></html>");

        var annotation = new AnnotationDto
        {
            XPath = """id("para1")""",
            EndingXPath = """id("para1")""",
            SelectedText = "Wolf",
            ChapterId = 0,
            VolumeId = 0,
            SeriesId = 0,
            LibraryId = 0,
            OwnerUserId = 0,
        };

        AnnotationHelper.InjectSingleElementAnnotations(doc, [annotation]);
        Assert.Equal(
            """Spice and    <app-epub-highlight id="epub-highlight-0" data-annotation-id="0">Wolf</app-epub-highlight> is       Amazing!""",
            doc.GetElementbyId("para1").InnerHtml
            );
    }

    [Fact]
    public void Test_InjectSingleElementAnnotations_WhitespacePositionsSelectOver()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("<html><body><p id='para1'>Spice and  Wolf is  Amazing!</p></body></html>");

        var annotation = new AnnotationDto
        {
            XPath = """id("para1")""",
            EndingXPath = """id("para1")""",
            // Selected text will not include those whitespaces by the way browsers work with selecting text
            SelectedText = "Spice and Wolf is Amazing!",
            ChapterId = 0,
            VolumeId = 0,
            SeriesId = 0,
            LibraryId = 0,
            OwnerUserId = 0,
        };

        AnnotationHelper.InjectSingleElementAnnotations(doc, [annotation]);
        Assert.Equal(
            """<app-epub-highlight id="epub-highlight-0" data-annotation-id="0">Spice and  Wolf is  Amazing!</app-epub-highlight>""",
            doc.GetElementbyId("para1").InnerHtml
        );
    }

    [Fact]
    public void Test_InjectSingleElementAnnotations_PreservesInlineElement()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("<html><body><p id='para1'>a <em>b</em> c</p></body></html>");

        var annotation = new AnnotationDto
        {
            XPath = """id("para1")""",
            EndingXPath = """id("para1")""",
            SelectedText = "a b",
            ChapterId = 0,
            VolumeId = 0,
            SeriesId = 0,
            LibraryId = 0,
            OwnerUserId = 0,
        };

        AnnotationHelper.InjectSingleElementAnnotations(doc, [annotation]);
        // The em must survive; the highlight covers its text from within, as one fragment per text run
        Assert.Equal(
            """<app-epub-highlight id="epub-highlight-0" data-annotation-id="0">a </app-epub-highlight><em><app-epub-highlight id="epub-highlight-0" data-annotation-id="0">b</app-epub-highlight></em> c""",
            doc.GetElementbyId("para1").InnerHtml
        );
    }

    [Fact]
    public void Test_InjectSingleElementAnnotations_PreservesFootnoteReference()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("<html><body><p id='para1'>text<sup><a href=\"#fn1\">1</a></sup> more</p></body></html>");

        var annotation = new AnnotationDto
        {
            XPath = """id("para1")""",
            EndingXPath = """id("para1")""",
            SelectedText = "1 more",
            ChapterId = 0,
            VolumeId = 0,
            SeriesId = 0,
            LibraryId = 0,
            OwnerUserId = 0,
        };

        AnnotationHelper.InjectSingleElementAnnotations(doc, [annotation]);
        // The anchor and its href must survive so the footnote is still reachable
        Assert.Equal(
            """text<sup><a href="#fn1"><app-epub-highlight id="epub-highlight-0" data-annotation-id="0">1</app-epub-highlight></a></sup><app-epub-highlight id="epub-highlight-0" data-annotation-id="0"> more</app-epub-highlight>""",
            doc.GetElementbyId("para1").InnerHtml
        );
    }

    [Fact]
    public void Test_InjectSingleElementAnnotations_StartsAndEndsInsideInlineElement()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("<html><body><p id='para1'>See <a href=\"#fn1\">note one</a> here</p></body></html>");

        var annotation = new AnnotationDto
        {
            XPath = """id("para1")""",
            EndingXPath = """id("para1")""",
            SelectedText = "ote on",
            ChapterId = 0,
            VolumeId = 0,
            SeriesId = 0,
            LibraryId = 0,
            OwnerUserId = 0,
        };

        AnnotationHelper.InjectSingleElementAnnotations(doc, [annotation]);
        Assert.Equal(
            """See <a href="#fn1">n<app-epub-highlight id="epub-highlight-0" data-annotation-id="0">ote on</app-epub-highlight>e</a> here""",
            doc.GetElementbyId("para1").InnerHtml
        );
    }

    [Fact]
    public void Test_InjectSingleElementAnnotations_TextNotFound_LeavesElementUntouched()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("<html><body><p id='para1'>a <a href=\"#fn1\">b</a> c</p></body></html>");

        var annotation = new AnnotationDto
        {
            XPath = """id("para1")""",
            EndingXPath = """id("para1")""",
            SelectedText = "does not exist",
            ChapterId = 0,
            VolumeId = 0,
            SeriesId = 0,
            LibraryId = 0,
            OwnerUserId = 0,
        };

        AnnotationHelper.InjectSingleElementAnnotations(doc, [annotation]);
        Assert.Equal(
            """a <a href="#fn1">b</a> c""",
            doc.GetElementbyId("para1").InnerHtml
        );
    }

    [Fact]
    public void Test_InjectSingleElementAnnotations_WhitespaceOnlyTextNode()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("<html><body><p id='para1'><em>a</em> <em>b</em></p></body></html>");

        var annotation = new AnnotationDto
        {
            XPath = """id("para1")""",
            EndingXPath = """id("para1")""",
            SelectedText = "a b",
            ChapterId = 0,
            VolumeId = 0,
            SeriesId = 0,
            LibraryId = 0,
            OwnerUserId = 0,
        };

        AnnotationHelper.InjectSingleElementAnnotations(doc, [annotation]);
        // The whitespace-only run between the two ems is part of the selection and must be highlighted
        Assert.Equal(
            """<em><app-epub-highlight id="epub-highlight-0" data-annotation-id="0">a</app-epub-highlight></em><app-epub-highlight id="epub-highlight-0" data-annotation-id="0"> </app-epub-highlight><em><app-epub-highlight id="epub-highlight-0" data-annotation-id="0">b</app-epub-highlight></em>""",
            doc.GetElementbyId("para1").InnerHtml
        );
    }

    [Fact]
    public void Test_InjectSingleElementAnnotations_OverlappingAnnotations()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("<html><body><p id='para1'>abcdefghij</p></body></html>");

        var first = new AnnotationDto
        {
            Id = 1,
            XPath = """id("para1")""",
            EndingXPath = """id("para1")""",
            SelectedText = "cdef",
            ChapterId = 0,
            VolumeId = 0,
            SeriesId = 0,
            LibraryId = 0,
            OwnerUserId = 0,
        };

        var second = new AnnotationDto
        {
            Id = 2,
            XPath = """id("para1")""",
            EndingXPath = """id("para1")""",
            SelectedText = "efgh",
            ChapterId = 0,
            VolumeId = 0,
            SeriesId = 0,
            LibraryId = 0,
            OwnerUserId = 0,
        };

        AnnotationHelper.InjectSingleElementAnnotations(doc, [first, second]);
        // Highlights cannot nest, so the overlap keeps the first annotation and the second only
        // highlights what is left. No text may be duplicated or consumed either way.
        Assert.Equal(
            """ab<app-epub-highlight id="epub-highlight-1" data-annotation-id="1">cdef</app-epub-highlight><app-epub-highlight id="epub-highlight-2" data-annotation-id="2">gh</app-epub-highlight>ij""",
            doc.GetElementbyId("para1").InnerHtml
        );
    }

    [Fact]
    public void Test_InjectSingleElementAnnotations_NewRangeOverExistingHighlight()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("<html><body><p id='para1'>abcdefghij</p></body></html>");

        var wider = new AnnotationDto
        {
            Id = 2,
            XPath = """id("para1")""",
            EndingXPath = """id("para1")""",
            SelectedText = "abcd",
            ChapterId = 0,
            VolumeId = 0,
            SeriesId = 0,
            LibraryId = 0,
            OwnerUserId = 0,
        };

        var later = new AnnotationDto
        {
            Id = 1,
            XPath = """id("para1")""",
            EndingXPath = """id("para1")""",
            SelectedText = "cdefgh",
            ChapterId = 0,
            VolumeId = 0,
            SeriesId = 0,
            LibraryId = 0,
            OwnerUserId = 0,
        };

        // Sorted by start position, so "abcd" is applied first despite being declared second
        AnnotationHelper.InjectSingleElementAnnotations(doc, [wider, later]);
        Assert.Equal(
            """<app-epub-highlight id="epub-highlight-2" data-annotation-id="2">abcd</app-epub-highlight><app-epub-highlight id="epub-highlight-1" data-annotation-id="1">efgh</app-epub-highlight>ij""",
            doc.GetElementbyId("para1").InnerHtml
        );
    }

    [Fact]
    public void Test_InjectSingleElementAnnotations_CharacterReferencePreserved()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("<html><body><p id='para1'>Spice &amp; Wolf</p></body></html>");

        var annotation = new AnnotationDto
        {
            XPath = """id("para1")""",
            EndingXPath = """id("para1")""",
            SelectedText = "Wolf",
            ChapterId = 0,
            VolumeId = 0,
            SeriesId = 0,
            LibraryId = 0,
            OwnerUserId = 0,
        };

        AnnotationHelper.InjectSingleElementAnnotations(doc, [annotation]);
        Assert.Equal(
            """Spice &amp; <app-epub-highlight id="epub-highlight-0" data-annotation-id="0">Wolf</app-epub-highlight>""",
            doc.GetElementbyId("para1").InnerHtml
        );
    }

    [Fact]
    public void Test_InjectSingleElementAnnotations_DoesNotSplitCharacterReference()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("<html><body><p id='para1'>Spice &amp; Wolf</p></body></html>");

        var annotation = new AnnotationDto
        {
            XPath = """id("para1")""",
            EndingXPath = """id("para1")""",
            // A browser selection renders "&amp;" as a single character, so this is what the user sees
            SelectedText = "Spice &",
            ChapterId = 0,
            VolumeId = 0,
            SeriesId = 0,
            LibraryId = 0,
            OwnerUserId = 0,
        };

        AnnotationHelper.InjectSingleElementAnnotations(doc, [annotation]);
        Assert.Equal(
            """<app-epub-highlight id="epub-highlight-0" data-annotation-id="0">Spice &amp;</app-epub-highlight> Wolf""",
            doc.GetElementbyId("para1").InnerHtml
        );
    }

    [Fact]
    public void Test_InjectSingleElementAnnotations_OutsideAnnotationLeftAlone()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("<html><body><p id='para1'><a>link</a> Spice &amp; Wolf <a>link</a></p></body></html>");

        var annotation = new AnnotationDto
        {
            XPath = """id("para1")""",
            EndingXPath = """id("para1")""",
            // The reader never selects the source form of an entity, only what it renders as
            SelectedText = "Spice & Wolf",
            ChapterId = 0,
            VolumeId = 0,
            SeriesId = 0,
            LibraryId = 0,
            OwnerUserId = 0,
        };

        AnnotationHelper.InjectSingleElementAnnotations(doc, [annotation]);
        Assert.Equal(
            """<a>link</a> <app-epub-highlight id="epub-highlight-0" data-annotation-id="0">Spice &amp; Wolf</app-epub-highlight> <a>link</a>""",
            doc.GetElementbyId("para1").InnerHtml
        );
    }

    [Fact]
    public void Test_InjectSingleElementAnnotations_MatchesNumericCharacterReference()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("<html><body><p id='para1'>don&#8217;t stop</p></body></html>");

        var annotation = new AnnotationDto
        {
            XPath = """id("para1")""",
            EndingXPath = """id("para1")""",
            SelectedText = "don’t",
            ChapterId = 0,
            VolumeId = 0,
            SeriesId = 0,
            LibraryId = 0,
            OwnerUserId = 0,
        };

        AnnotationHelper.InjectSingleElementAnnotations(doc, [annotation]);
        Assert.Equal(
            """<app-epub-highlight id="epub-highlight-0" data-annotation-id="0">don&#8217;t</app-epub-highlight> stop""",
            doc.GetElementbyId("para1").InnerHtml
        );
    }

    [Fact]
    public void Test_InjectMultiElementAnnotations_AcrossTwoParagraphs()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("<html><body><div><p id='p1'>Hello</p><p id='p2'>World</p></div></body></html>");

        var annotation = new AnnotationDto
        {
            XPath = """id("p1")""",
            EndingXPath = """id("p2")""",
            SelectedText = "lo Wor",
            ChapterId = 0,
            VolumeId = 0,
            SeriesId = 0,
            LibraryId = 0,
            OwnerUserId = 0,
        };

        AnnotationHelper.InjectMultiElementAnnotations(doc, [annotation]);
        Assert.Equal(
            """Hel<app-epub-highlight id="epub-highlight-0" data-annotation-id="0">lo</app-epub-highlight>""",
            doc.GetElementbyId("p1").InnerHtml
        );
        // The element separator must be accounted for, otherwise the whole of World is highlighted
        Assert.Equal(
            """<app-epub-highlight id="epub-highlight-0" data-annotation-id="0">Wor</app-epub-highlight>ld""",
            doc.GetElementbyId("p2").InnerHtml
        );
    }

    [Fact]
    public void Test_InjectMultiElementAnnotations_StartingInsideInlineElement()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("<html><body><div><p id='p1'>A<sup><a href=\"#fn1\">1</a></sup> alpha</p><p id='p2'>beta gamma</p></div></body></html>");

        var annotation = new AnnotationDto
        {
            XPath = """//body/div[1]/p[1]/sup[1]/a[1]""",
            EndingXPath = """id("p2")""",
            SelectedText = "1 alpha beta",
            ChapterId = 0,
            VolumeId = 0,
            SeriesId = 0,
            LibraryId = 0,
            OwnerUserId = 0,
        };

        // sup must normalize to the paragraph, otherwise the range walk never reaches p2 and nothing is injected
        AnnotationHelper.InjectMultiElementAnnotations(doc, [annotation]);
        Assert.Equal(
            """A<sup><a href="#fn1"><app-epub-highlight id="epub-highlight-0" data-annotation-id="0">1</app-epub-highlight></a></sup><app-epub-highlight id="epub-highlight-0" data-annotation-id="0"> alpha</app-epub-highlight>""",
            doc.GetElementbyId("p1").InnerHtml
        );
        Assert.Equal(
            """<app-epub-highlight id="epub-highlight-0" data-annotation-id="0">beta</app-epub-highlight> gamma""",
            doc.GetElementbyId("p2").InnerHtml
        );
    }

    [Fact]
    public void Test_InjectMultiElementAnnotations_MatchesEntityEncodedText()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("<html><body><div><p id='p1'>Hello</p><p id='p2'>Tom &amp; Jerry</p></div></body></html>");

        var annotation = new AnnotationDto
        {
            XPath = """id("p1")""",
            EndingXPath = """id("p2")""",
            // The selection ends on the rendered entity, which spans the block boundary in raw text
            SelectedText = "Hello Tom &",
            ChapterId = 0,
            VolumeId = 0,
            SeriesId = 0,
            LibraryId = 0,
            OwnerUserId = 0,
        };

        // Both elements are mapped into the raw text, so the reference stays whole
        AnnotationHelper.InjectMultiElementAnnotations(doc, [annotation]);
        Assert.Equal(
            """<app-epub-highlight id="epub-highlight-0" data-annotation-id="0">Hello</app-epub-highlight>""",
            doc.GetElementbyId("p1").InnerHtml
        );
        Assert.Equal(
            """<app-epub-highlight id="epub-highlight-0" data-annotation-id="0">Tom &amp;</app-epub-highlight> Jerry""",
            doc.GetElementbyId("p2").InnerHtml
        );
    }

}
