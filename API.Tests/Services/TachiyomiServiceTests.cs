using API.Helpers.Builders;
using API.Services.Plus;
using API.Services.Reading;
using Xunit.Abstractions;

namespace API.Tests.Services;
using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using System.Threading.Tasks;
using Data;
using Data.Repositories;
using API.Entities;
using API.Entities.Enums;
using API.Services;
using SignalR;
using AutoMapper;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

public class TachiyomiServiceTests(ITestOutputHelper outputHelper): AbstractDbTest(outputHelper)
{


    public static (IReaderService, ITachiyomiService) Setup(IUnitOfWork unitOfWork, IMapper mapper)
    {
        var readerService = new ReaderService(unitOfWork, Substitute.For<ILogger<ReaderService>>(),
            Substitute.For<IEventHub>(), Substitute.For<IImageService>(),
            new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new MockFileSystem()),
            Substitute.For<IScrobblingService>(), Substitute.For<IReadingSessionService>(),
            Substitute.For<IClientInfoAccessor>(), Substitute.For<ISeriesService>(), Substitute.For<IEntityNamingService>(),
            Substitute.For<ILocalizationService>());
        var tachiyomiService = new TachiyomiService(unitOfWork, mapper, Substitute.For<ILogger<TachiyomiService>>(), readerService);

        return (readerService, tachiyomiService);
    }


    #region GetLatestChapter

    [Fact]
    public async Task GetLatestChapter_ShouldReturnChapter_NoProgress()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readerService, tachiyomiService) = Setup(unitOfWork, mapper);

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("95").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("96").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("3").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("4").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("3")
                .WithChapter(new ChapterBuilder("31").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("32").WithPages(1).Build())
                .Build())
            .WithPages(7)
            .Build();

        var library = new LibraryBuilder("Test LIb", LibraryType.Manga)
            .WithSeries(series)
            .Build();


        context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            Libraries = new List<Library>()
            {
                library
            }

        });
        await context.SaveChangesAsync();

        var latestChapter = await tachiyomiService.GetLatestChapter(1, 1);

        Assert.Null(latestChapter);
    }

    [Fact]
    public async Task GetLatestChapter_ShouldReturnMaxChapter_CompletelyRead()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readerService, tachiyomiService) = Setup(unitOfWork, mapper);

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("95").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("96").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("3").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("4").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("3")
                .WithChapter(new ChapterBuilder("31").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("32").WithPages(1).Build())
                .Build())
            .WithPages(7)
            .Build();

        var library = new LibraryBuilder("Test LIb", LibraryType.Manga)
            .WithSeries(series)
            .Build();

        context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            Libraries = new List<Library>()
            {
                library
            }

        });
        await context.SaveChangesAsync();


        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);
        await readerService.MarkSeriesAsRead(user,1);

        await context.SaveChangesAsync();


        var latestChapter = await tachiyomiService.GetLatestChapter(1, 1);

        Assert.Equal("96", latestChapter.Number);
    }

    [Fact]
    public async Task GetLatestChapter_ShouldReturnHighestChapter_Progress()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readerService, tachiyomiService) = Setup(unitOfWork, mapper);

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("95").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("96").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("21").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("22").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("3")
                .WithChapter(new ChapterBuilder("31").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("32").WithPages(1).Build())
                .Build())
            .WithPages(7)
            .Build();

        var library = new LibraryBuilder("Test LIb", LibraryType.Manga)
            .WithSeries(series)
            .Build();

        context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            Libraries = new List<Library>()
            {
                library
            }

        });
        await context.SaveChangesAsync();


        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);
        await tachiyomiService.MarkChaptersUntilAsRead(user,1,21);

        await context.SaveChangesAsync();


        var latestChapter = await tachiyomiService.GetLatestChapter(1, 1);

        Assert.Equal("21", latestChapter.Number);
    }

    [Fact]
    public async Task GetLatestChapter_ShouldReturnEncodedVolume_Progress()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readerService, tachiyomiService) = Setup(unitOfWork, mapper);

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("95").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("96").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("21").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("22").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("3")
                .WithChapter(new ChapterBuilder("31").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("32").WithPages(1).Build())
                .Build())
            .WithPages(7)
            .Build();

        var library = new LibraryBuilder("Test LIb", LibraryType.Manga)
            .WithSeries(series)
            .Build();

        context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            Libraries = new List<Library>()
            {
                library
            }

        });
        await context.SaveChangesAsync();


        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);

        await tachiyomiService.MarkChaptersUntilAsRead(user,1,1/10_000F);

        await context.SaveChangesAsync();


        var latestChapter = await tachiyomiService.GetLatestChapter(1, 1);
        Assert.Equal("0.0001", latestChapter.Number);
    }

    [Fact]
    public async Task GetLatestChapter_ShouldReturnEncodedVolume_Progress2()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readerService, tachiyomiService) = Setup(unitOfWork, mapper);

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)
                    .WithPages(199).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)
                    .WithPages(192).Build())
                .Build())
            .WithVolume(new VolumeBuilder("3")
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)
                    .WithPages(255).Build())
                .Build())
            .WithPages(646)
            .Build();

        var library = new LibraryBuilder("Test LIb", LibraryType.Manga)
            .WithSeries(series)
            .Build();

        context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            Libraries = new List<Library>()
            {
                library
            }

        });
        await context.SaveChangesAsync();


        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);

        await readerService.MarkSeriesAsRead(user, 1);

        await context.SaveChangesAsync();


        var latestChapter = await tachiyomiService.GetLatestChapter(1, 1);
        Assert.Equal("0.0003", latestChapter.Number);
    }


    [Fact]
    public async Task GetLatestChapter_ShouldReturnEncodedYearlyVolume_Progress()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readerService, tachiyomiService) = Setup(unitOfWork, mapper);

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("95").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("96").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("1997")
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2002")
                .WithChapter(new ChapterBuilder("2").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2005")
                .WithChapter(new ChapterBuilder("3").WithPages(1).Build())
                .Build())
            .WithPages(7)
            .Build();

        var library = new LibraryBuilder("Test LIb", LibraryType.Manga)
            .WithSeries(series)
            .Build();

        context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            Libraries = new List<Library>()
            {
                library
            }

        });
        await context.SaveChangesAsync();

        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);

        await tachiyomiService.MarkChaptersUntilAsRead(user,1,2002/10_000F);

        await context.SaveChangesAsync();


        var latestChapter = await tachiyomiService.GetLatestChapter(1, 1);
        Assert.Equal("0.2002", latestChapter.Number);
    }

    #endregion


    #region MarkChaptersUntilAsRead

    [Fact]
    public async Task MarkChaptersUntilAsRead_ShouldReturnChapter_NoProgress()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readerService, tachiyomiService) = Setup(unitOfWork, mapper);

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("95").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("96").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("3").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("4").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("3")
                .WithChapter(new ChapterBuilder("31").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("32").WithPages(1).Build())
                .Build())
            .WithPages(7)
            .Build();

        var library = new LibraryBuilder("Test LIb", LibraryType.Manga)
            .WithSeries(series)
            .Build();

        context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            Libraries = new List<Library>()
            {
                library
            }

        });
        await context.SaveChangesAsync();

        var latestChapter = await tachiyomiService.GetLatestChapter(1, 1);

        Assert.Null(latestChapter);
    }
    [Fact]
    public async Task MarkChaptersUntilAsRead_ShouldReturnMaxChapter_CompletelyRead()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readerService, tachiyomiService) = Setup(unitOfWork, mapper);

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("95").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("96").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("3").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("4").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("3")
                .WithChapter(new ChapterBuilder("31").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("32").WithPages(1).Build())
                .Build())
            .WithPages(7)
            .Build();

        var library = new LibraryBuilder("Test LIb", LibraryType.Manga)
            .WithSeries(series)
            .Build();

        context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            Libraries = new List<Library>()
            {
                library
            }

        });
        await context.SaveChangesAsync();

        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);
        await readerService.MarkSeriesAsRead(user,1);

        await context.SaveChangesAsync();


        var latestChapter = await tachiyomiService.GetLatestChapter(1, 1);

        Assert.Equal("96", latestChapter.Number);
    }

    [Fact]
    public async Task MarkChaptersUntilAsRead_ShouldReturnHighestChapter_Progress()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readerService, tachiyomiService) = Setup(unitOfWork, mapper);

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("95").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("96").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("21").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("23").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("3")
                .WithChapter(new ChapterBuilder("31").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("32").WithPages(1).Build())
                .Build())
            .WithPages(7)
            .Build();

        var library = new LibraryBuilder("Test LIb", LibraryType.Manga)
            .WithSeries(series)
            .Build();

        context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            Libraries = new List<Library>()
            {
                library
            }

        });
        await context.SaveChangesAsync();

        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);
        await tachiyomiService.MarkChaptersUntilAsRead(user,1,21);

        await context.SaveChangesAsync();


        var latestChapter = await tachiyomiService.GetLatestChapter(1, 1);

        Assert.Equal("21", latestChapter.Number);
    }
    [Fact]
    public async Task MarkChaptersUntilAsRead_ShouldReturnEncodedVolume_Progress()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (readerService, tachiyomiService) = Setup(unitOfWork, mapper);
        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("95").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("96").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("21").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("23").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("3")
                .WithChapter(new ChapterBuilder("31").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("32").WithPages(1).Build())
                .Build())
            .WithPages(7)
            .Build();

        var library = new LibraryBuilder("Test LIb", LibraryType.Manga)
            .WithSeries(series)
            .Build();

        context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            Libraries = new List<Library>()
            {
                library
            }

        });
        await context.SaveChangesAsync();

        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);

        await tachiyomiService.MarkChaptersUntilAsRead(user,1,1/10_000F);

        await context.SaveChangesAsync();


        var latestChapter = await tachiyomiService.GetLatestChapter(1, 1);
        Assert.Equal("0.0001", latestChapter.Number);
    }

    #endregion

}
