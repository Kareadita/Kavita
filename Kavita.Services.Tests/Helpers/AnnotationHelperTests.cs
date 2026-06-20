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
            """　<app-epub-highlight id="epub-highlight-0">意識が芽生</app-epub-highlight>えてから二日が経過した。""",
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
            """Spice and    <app-epub-highlight id="epub-highlight-0">Wolf</app-epub-highlight> is       Amazing!""",
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
            """<app-epub-highlight id="epub-highlight-0">Spice and  Wolf is  Amazing!</app-epub-highlight>""",
            doc.GetElementbyId("para1").InnerHtml
        );
    }

}
