using System.IO.Abstractions;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.Models.Entities.Enums;
using Kavita.Services.Builders;
using NSubstitute;

namespace Kavita.Services.Tests;

public class DownloadServiceTests
{
    [Fact]
    public async Task DownloadChapter_Kepub_ConversionFails_ReturnsEpubPath()
    {
        const string epubPath = "/books/a.epub";
        var kepubify = Substitute.For<IKepubifyConverterService>();
        kepubify.BuildKepubifyPath(Arg.Any<int>()).Returns("/temp/a.kepub.epub");
        kepubify.ConvertEpubToKepub(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var service = new DownloadService(Substitute.For<ICacheService>(), Substitute.For<ILocalizationService>(),
            Substitute.For<IDirectoryService>(), kepubify, Substitute.For<IFileSystem>(), Substitute.For<IEntityNamingService>(),
            Substitute.For<IUnitOfWork>());
        var chapter = new ChapterBuilder("1")
            .WithFile(new MangaFileBuilder(epubPath, MangaFormat.Epub).Build())
            .Build();

        Assert.Equal(epubPath, await service.DownloadChapter(1, chapter, DownloadFormat.Kepub, CancellationToken.None));
    }
}
