using System.IO.Abstractions;
using System.Xml;
using Kavita.API.Services;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Metadata;
using Kavita.Models.Parser;
using Kavita.Services.Reading;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Kavita.Services.Tests;

public class ReadingItemServiceTests
{
    private const string EpubPath = "B:/Fiction/Martha Wells/The Murderbot Diaries/Fugitive Telemetry.epub";
    private const string ArchivePath = "M:/Manga/Accel World/Accel World Vol. 1.cbz";

    private readonly IBookService _bookService = Substitute.For<IBookService>();
    private readonly IArchiveService _archiveService = Substitute.For<IArchiveService>();
    private readonly ReadingItemService _readingItemService;

    public ReadingItemServiceTests()
    {
        var directoryService = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new FileSystem());
        _readingItemService = new ReadingItemService(_archiveService, _bookService, Substitute.For<IImageService>(),
            directoryService, Substitute.For<ILogger<ReadingItemService>>());
    }

    private static ParserInfo MurderbotInfo() => new()
    {
        Series = "The Murderbot Diaries",
        Title = "Fugitive Telemetry",
        Filename = "Fugitive Telemetry.epub",
        FullFilePath = EpubPath,
        Format = MangaFormat.Epub,
        Volumes = Scanner.Parser.LooseLeafVolume,
        Chapters = Scanner.Parser.DefaultChapter,
    };

    private ParseFileResult ParseEpub() =>
        _readingItemService.ParseFile(EpubPath, "B:/Fiction/Martha Wells/The Murderbot Diaries", "B:/Fiction", LibraryType.Book, true);

    [Fact]
    public void ParseFile_UnreadableComicInfo_IsImportedWithAnIssue()
    {
        _archiveService.GetComicInfo(ArchivePath).Throws(new InvalidDataException("End of Central Directory record could not be found."));

        var result = _readingItemService.ParseFile(ArchivePath, "M:/Manga/Accel World", "M:/Manga", LibraryType.Manga, true);

        Assert.Equal("Accel World", result.Info?.Series);
        Assert.Equal(MediaErrorReason.MetadataUnreadable, result.Issue?.Reason);
        Assert.Equal("End of Central Directory record could not be found.", result.Issue?.Details);
    }

    [Fact]
    public void ParseFile_EpubOnlyOpensLeniently_IsImportedWithAnIssue()
    {
        _bookService.GetComicInfo(EpubPath, out Arg.Any<string?>()).Returns(ci =>
        {
            ci[1] = "navigation file is not a valid XHTML file";
            return new ComicInfo();
        });
        _bookService.ParseInfo(EpubPath).Returns(MurderbotInfo());

        var result = ParseEpub();

        Assert.NotNull(result.Info);
        Assert.Equal(MediaErrorReason.EpubNotStrict, result.Issue?.Reason);
        Assert.Equal("navigation file is not a valid XHTML file", result.Issue?.Details);
    }

    [Fact]
    public void ParseFile_EpubThatCannotBeOpened_FailsWithTheCause()
    {
        var cause = new InvalidDataException("EPUB parsing error", new XmlException("'calibre' is an undeclared prefix."));
        _bookService.GetComicInfo(EpubPath, out Arg.Any<string?>()).Throws(cause);
        _bookService.ParseInfo(EpubPath).Throws(cause);

        var result = ParseEpub();

        Assert.True(result.IsFailed);
        Assert.Equal(MediaErrorReason.CorruptEpub, result.Issue?.Reason);
        Assert.Equal("EPUB parsing error -> 'calibre' is an undeclared prefix.", result.Issue?.Details);
    }

    [Fact]
    public void ParseFile_ValidEpub_HasNoIssue()
    {
        _bookService.GetComicInfo(EpubPath, out Arg.Any<string?>()).Returns(new ComicInfo());
        _bookService.ParseInfo(EpubPath).Returns(MurderbotInfo());

        var result = ParseEpub();

        Assert.NotNull(result.Info);
        Assert.Null(result.Issue);
    }
}
