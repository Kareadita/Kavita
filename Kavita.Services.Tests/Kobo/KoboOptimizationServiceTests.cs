using System.IO.Abstractions;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.Kobo;
using Kavita.API.Services.Reading;
using Kavita.Models.DTOs;
using Kavita.Models.Entities.Enums;
using Kavita.Services.Kobo;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Kavita.Services.Tests.Kobo;

public class KoboOptimizationServiceTests
{
    [Fact]
    public async Task GetKoboDownloadPathAsync_Epub_ConversionFails_ReturnsEpubPath()
    {
        const string epubPath = "/books/a.epub";
        var kepubify = Substitute.For<IKepubifyConverterService>();
        kepubify.BuildKepubifyPath(Arg.Any<int>()).Returns("/temp/a.kepub.epub");
        kepubify.ConvertEpubToKepub(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var chapterRepository = Substitute.For<IChapterRepository>();
        chapterRepository.GetChapterDtoAsync(1, 1, Arg.Any<CancellationToken>()).Returns(new ChapterDto
        {
            Id = 1,
            Files = [new MangaFileDto { FilePath = epubPath, Format = MangaFormat.Epub }],
        });
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.ChapterRepository.Returns(chapterRepository);

        var service = new KoboOptimizationService(Substitute.For<ICacheService>(), Substitute.For<ILocalizationService>(),
            Substitute.For<IDirectoryService>(), kepubify, Substitute.For<IFileSystem>(), Substitute.For<IEntityNamingService>(),
            unitOfWork, Substitute.For<IEpubBuilderService>(), Substitute.For<IReadingProfileService>(),
            Substitute.For<ILogger<KoboOptimizationService>>());

        Assert.Equal(epubPath, await service.GetKoboDownloadPathAsync(1, 1, true, null, CancellationToken.None));
    }
}
