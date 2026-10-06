using System.Xml;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Parser;
using Kavita.Services.Scanner;

namespace Kavita.Services.Tests;

public class ParseIssuesTests
{
    [Theory]
    [InlineData("a.epub", typeof(IOException), MediaErrorReason.IoError)]
    [InlineData("a.cbz", typeof(FileNotFoundException), MediaErrorReason.IoError)]
    [InlineData("a.epub", typeof(UnauthorizedAccessException), MediaErrorReason.IoError)]
    [InlineData("a.epub", typeof(EndOfStreamException), MediaErrorReason.CorruptEpub)]
    [InlineData("a.cbz", typeof(EndOfStreamException), MediaErrorReason.UnreadableArchive)]
    [InlineData("a.epub", typeof(InvalidDataException), MediaErrorReason.CorruptEpub)]
    [InlineData("a.cbz", typeof(InvalidDataException), MediaErrorReason.UnreadableArchive)]
    [InlineData("a.pdf", typeof(InvalidDataException), MediaErrorReason.Unknown)]
    public void ReasonFor_Exception(string filePath, Type exceptionType, MediaErrorReason expected)
    {
        Assert.Equal(expected, ParseIssues.ReasonFor(filePath, (Exception) Activator.CreateInstance(exceptionType)!));
    }

    [Fact]
    public void FromFailedParse_NothingParsed_IsParseFailed()
    {
        Assert.Equal(MediaErrorReason.ParseFailed, ParseIssues.FromFailedParse(null).Reason);
    }

    [Fact]
    public void FromFailedParse_ParsedWithoutSeries_IsNoSeriesName()
    {
        Assert.Equal(MediaErrorReason.NoSeriesName, ParseIssues.FromFailedParse(new ParserInfo { Series = string.Empty }).Reason);
    }

    [Fact]
    public void Describe_IncludesInnerExceptions()
    {
        var ex = new InvalidDataException("EPUB parsing error: navigation file is not a valid XHTML file.",
            new XmlException("'calibre' is an undeclared prefix."));

        Assert.Equal("EPUB parsing error: navigation file is not a valid XHTML file. -> 'calibre' is an undeclared prefix.",
            ParseIssues.Describe(ex));
    }
}
