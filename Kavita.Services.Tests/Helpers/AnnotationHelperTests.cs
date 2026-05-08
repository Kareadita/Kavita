using HtmlAgilityPack;
using Kavita.Models.DTOs.Reader;
using Kavita.Services.Helpers;

namespace Kavita.Services.Tests.Helpers;

public class AnnotationHelperTests
{

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

}
