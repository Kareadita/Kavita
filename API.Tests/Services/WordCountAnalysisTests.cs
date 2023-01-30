using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using API.Services;
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
    private const long WordCount = 37417;
    private const long MinHoursToRead = 1;
    private const long AvgHoursToRead = 2;
    private const long MaxHoursToRead = 4;
    public WordCountAnalysisTests() : base()
    {
        _readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>(),
            Substitute.For<IEventHub>());
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
        var series = EntityFactory.CreateSeries("Test Series");
        series.Format = MangaFormat.Epub;
        var chapter = EntityFactory.CreateChapter("", false, new List<MangaFile>()
        {
            EntityFactory.CreateMangaFile(
                Path.Join(_testDirectory, "The Golden Harpoon; Or, Lost Among the Floes A Story of the Whaling Grounds.epub"),
                MangaFormat.Epub, 0)
        });

        _context.Library.Add(new Library()
        {
            Name = "Test",
            Type = LibraryType.Book,
            Series = new List<Series>() {series}
        });

        series.Volumes = new List<Volume>()
        {
            EntityFactory.CreateVolume("0", new List<Chapter>() {chapter})
        };

        await _context.SaveChangesAsync();


        var cacheService = new CacheHelper(new FileService());
        var service = new WordCountAnalyzerService(Substitute.For<ILogger<WordCountAnalyzerService>>(), _unitOfWork,
            Substitute.For<IEventHub>(), cacheService, _readerService);


        await service.ScanSeries(1, 1);

        Assert.Equal(WordCount, series.WordCount);
        Assert.Equal(MinHoursToRead, series.MinHoursToRead);
        Assert.Equal(AvgHoursToRead, series.AvgHoursToRead);
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
        var series = EntityFactory.CreateSeries("Test Series");
        series.Format = MangaFormat.Epub;
        var chapter = EntityFactory.CreateChapter("", false, new List<MangaFile>()
        {
            EntityFactory.CreateMangaFile(
                Path.Join(_testDirectory, "The Golden Harpoon; Or, Lost Among the Floes A Story of the Whaling Grounds.epub"),
                MangaFormat.Epub, 0)
        });

        _context.Library.Add(new Library()
        {
            Name = "Test",
            Type = LibraryType.Book,
            Series = new List<Series>() {series}
        });

        series.Volumes = new List<Volume>()
        {
            EntityFactory.CreateVolume("0", new List<Chapter>() {chapter})
        };

        await _context.SaveChangesAsync();


        var cacheService = new CacheHelper(new FileService());
        var service = new WordCountAnalyzerService(Substitute.For<ILogger<WordCountAnalyzerService>>(), _unitOfWork,
            Substitute.For<IEventHub>(), cacheService, _readerService);


        await service.ScanSeries(1, 1);

        var chapter2 = EntityFactory.CreateChapter("2", false, new List<MangaFile>()
        {
            EntityFactory.CreateMangaFile(
                Path.Join(_testDirectory, "The Golden Harpoon; Or, Lost Among the Floes A Story of the Whaling Grounds.epub"),
                MangaFormat.Epub, 0)
        });

        series.Volumes.First().Chapters.Add(chapter2);
        await _unitOfWork.CommitAsync();

        await service.ScanSeries(1, 1);

        Assert.Equal(WordCount * 2L, series.WordCount);
        Assert.Equal(MinHoursToRead * 2, series.MinHoursToRead);
        Assert.Equal(AvgHoursToRead * 2, series.AvgHoursToRead);
        Assert.Equal((MaxHoursToRead * 2) - 1, series.MaxHoursToRead); // This is just a rounding issue

        // Validate the Chapter gets updated correctly
        var volume = series.Volumes.First();
        Assert.Equal(WordCount * 2L, volume.WordCount);
        Assert.Equal(MinHoursToRead * 2, volume.MinHoursToRead);
        Assert.Equal(AvgHoursToRead * 2, volume.AvgHoursToRead);
        Assert.Equal((MaxHoursToRead * 2) - 1, volume.MaxHoursToRead);

        Assert.Equal(WordCount, chapter.WordCount);
        Assert.Equal(MinHoursToRead, chapter.MinHoursToRead);
        Assert.Equal(AvgHoursToRead, chapter.AvgHoursToRead);
        Assert.Equal(MaxHoursToRead, chapter.MaxHoursToRead);
    }


}
