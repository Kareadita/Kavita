using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers;
using API.Helpers.Builders;
using API.Services;
using API.Services.Plus;
using API.Services.Tasks;
using API.Services.Tasks.Metadata;
using API.SignalR;
using API.Tests.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Services;

public class WordCountAnalysisTests : AbstractDbTest
{
    private readonly IReaderService _readerService;
    private readonly string _testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/BookService");
    private const long WordCount = 33608; // 37417 if splitting on space, 33608 if just character count
    private const long MinHoursToRead = 1;
    private const float AvgHoursToRead = 1.66954792f;
    private const long MaxHoursToRead = 3;
    public WordCountAnalysisTests() : base()
    {
        _readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>(),
            Substitute.For<IEventHub>(), Substitute.For<IImageService>(),
            new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new MockFileSystem()),
            Substitute.For<IScrobblingService>());
    }

    protected override async Task ResetDb()
    {
        _context.Series.RemoveRange(_context.Series.ToList());

        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task ReadingTimeShouldBeNonZero()
    {
        await ResetDb();
        var series = new SeriesBuilder("Test Series")
            .WithFormat(MangaFormat.Epub)
            .Build();

        var chapter = new ChapterBuilder("")
            .WithFile(new MangaFileBuilder(
                Path.Join(_testDirectory,
                    "The Golden Harpoon; Or, Lost Among the Floes A Story of the Whaling Grounds.epub"),
                MangaFormat.Epub).Build())
            .Build();

        _context.Library.Add(new LibraryBuilder("Test LIb", LibraryType.Book)
            .WithSeries(series)
            .Build());

        series.Volumes = new List<Volume>()
        {
            new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(chapter)
                .Build(),
        };

        await _context.SaveChangesAsync();


        var cacheService = new CacheHelper(new FileService());
        var service = new WordCountAnalyzerService(Substitute.For<ILogger<WordCountAnalyzerService>>(), _unitOfWork,
            Substitute.For<IEventHub>(), cacheService, _readerService, Substitute.For<IMediaErrorService>());


        await service.ScanSeries(1, 1);

        Assert.Equal(WordCount, series.WordCount);
        Assert.Equal(MinHoursToRead, series.MinHoursToRead);
        Assert.True(series.AvgHoursToRead.Is(AvgHoursToRead));
        Assert.Equal(MaxHoursToRead, series.MaxHoursToRead);

        // Validate the Chapter gets updated correctly
        var volume = series.Volumes.First();
        Assert.Equal(WordCount, volume.WordCount);
        Assert.Equal(MinHoursToRead, volume.MinHoursToRead);
        Assert.Equal(AvgHoursToRead, volume.AvgHoursToRead);
        Assert.Equal(MaxHoursToRead, volume.MaxHoursToRead);

        Assert.Equal(WordCount, chapter.WordCount);
        Assert.Equal(MinHoursToRead, chapter.MinHoursToRead);
        Assert.Equal(AvgHoursToRead, chapter.AvgHoursToRead);
        Assert.Equal(MaxHoursToRead, chapter.MaxHoursToRead);
    }



    [Fact]
    public async Task ReadingTimeShouldIncreaseWhenNewBookAdded()
    {
        await ResetDb();
        var chapter = new ChapterBuilder("")
            .WithFile(new MangaFileBuilder(
                Path.Join(_testDirectory,
                    "The Golden Harpoon; Or, Lost Among the Floes A Story of the Whaling Grounds.epub"),
                MangaFormat.Epub).Build())
            .Build();
        var series = new SeriesBuilder("Test Series")
            .WithFormat(MangaFormat.Epub)
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(chapter)
                .Build())
            .Build();

        _context.Library.Add(new LibraryBuilder("Test", LibraryType.Book)
            .WithSeries(series)
            .Build());


        await _context.SaveChangesAsync();


        var cacheService = new CacheHelper(new FileService());
        var service = new WordCountAnalyzerService(Substitute.For<ILogger<WordCountAnalyzerService>>(), _unitOfWork,
            Substitute.For<IEventHub>(), cacheService, _readerService, Substitute.For<IMediaErrorService>());
        await service.ScanSeries(1, 1);

        var chapter2 = new ChapterBuilder("2")
            .WithFile(new MangaFileBuilder(
                Path.Join(_testDirectory,
                    "The Golden Harpoon; Or, Lost Among the Floes A Story of the Whaling Grounds.epub"),
                MangaFormat.Epub).Build())
            .Build();


        series.Volumes.Add(new VolumeBuilder("1")
            .WithChapter(chapter2)
            .Build());

        series.Volumes.First().Chapters.Add(chapter2);
        await _unitOfWork.CommitAsync();

        await service.ScanSeries(1, 1);

        Assert.Equal(WordCount * 2L, series.WordCount);
        Assert.Equal(MinHoursToRead * 2, series.MinHoursToRead);

        var firstVolume = series.Volumes.ElementAt(0);
        Assert.Equal(WordCount, firstVolume.WordCount);
        Assert.Equal(MinHoursToRead, firstVolume.MinHoursToRead);
        Assert.True(series.AvgHoursToRead.Is(AvgHoursToRead * 2));
        Assert.Equal(MaxHoursToRead, firstVolume.MaxHoursToRead);

        var secondVolume = series.Volumes.ElementAt(1);
        Assert.Equal(WordCount, secondVolume.WordCount);
        Assert.Equal(MinHoursToRead, secondVolume.MinHoursToRead);
        Assert.Equal(AvgHoursToRead, secondVolume.AvgHoursToRead);
        Assert.Equal(MaxHoursToRead, secondVolume.MaxHoursToRead);

        // Validate original chapter doesn't change
        Assert.Equal(WordCount, chapter.WordCount);
        Assert.Equal(MinHoursToRead, chapter.MinHoursToRead);
        Assert.Equal(AvgHoursToRead, chapter.AvgHoursToRead);
        Assert.Equal(MaxHoursToRead, chapter.MaxHoursToRead);

        // Validate new chapter gets updated
        Assert.Equal(WordCount, chapter2.WordCount);
        Assert.Equal(MinHoursToRead, chapter2.MinHoursToRead);
        Assert.Equal(AvgHoursToRead, chapter2.AvgHoursToRead);
        Assert.Equal(MaxHoursToRead, chapter2.MaxHoursToRead);
    }


}
