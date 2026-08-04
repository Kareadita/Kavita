using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.Database;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.Constants;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.User;
using Kavita.Models.Entities.Progress;
using Kavita.Models.Entities.User;
using Kavita.Services.Builders;
using Kavita.Services.Kobo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Services.Tests.Kobo;

public class KoboLocationRematchServiceTests(ITestOutputHelper testOutputHelper) : AbstractDbTest(testOutputHelper)
{
    [Fact]
    public async Task Rematch_KeepsLocation_WhenBookScrollIdMapsToNewFile()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (user, chapter) = await SeedChapter(unitOfWork, context);

        context.AppUserProgresses.Add(new AppUserProgress
        {
            AppUserId = user.Id,
            ChapterId = chapter.Id,
            VolumeId = chapter.VolumeId,
            SeriesId = chapter.Volume.SeriesId,
            LibraryId = chapter.Volume.Series.LibraryId,
            PagesRead = 4,
            BookScrollId = "id(\"kobo.1.2\")",
        });
        context.AppUserKoboReadingLocation.Add(new AppUserKoboReadingLocation
        {
            AppUserId = user.Id,
            ChapterId = chapter.Id,
            LocationValue = "epub-era-span",
            LocationType = "KoboSpan",
            LocationSource = "OEBPS/old.xhtml",
        });
        await context.SaveChangesAsync();

        var mapper = Substitute.For<IKoboLocationMapper>();
        mapper.TryMapBookScrollIdToLocationAsync(
                "/cache/book.kepub.epub", 4, "id(\"kobo.1.2\")", Arg.Any<CancellationToken>())
            .Returns(new KoboMappedLocation("kobo.9.1", "KoboSpan", "OEBPS/ch.xhtml"));

        var rematch = new KoboLocationRematchService(unitOfWork, mapper,
            Substitute.For<ILogger<KoboLocationRematchService>>());
        await rematch.RematchAfterDeviceFileChangeAsync(chapter.Id, "/cache/book.kepub.epub");

        var location = Assert.Single(context.AppUserKoboReadingLocation);
        Assert.Equal("kobo.9.1", location.LocationValue);
        Assert.Equal("OEBPS/ch.xhtml", location.LocationSource);

        var progress = Assert.Single(context.AppUserProgresses);
        Assert.Equal(4, progress.PagesRead);
        Assert.Equal("id(\"kobo.1.2\")", progress.BookScrollId);
    }

    [Fact]
    public async Task Rematch_ClearsLocation_WhenMapFails_KeepsBookScrollIdAndPercent()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (user, chapter) = await SeedChapter(unitOfWork, context);

        context.AppUserProgresses.Add(new AppUserProgress
        {
            AppUserId = user.Id,
            ChapterId = chapter.Id,
            VolumeId = chapter.VolumeId,
            SeriesId = chapter.Volume.SeriesId,
            LibraryId = chapter.Volume.Series.LibraryId,
            PagesRead = 7,
            BookScrollId = "//body/p[2]",
        });
        context.AppUserKoboReadingLocation.Add(new AppUserKoboReadingLocation
        {
            AppUserId = user.Id,
            ChapterId = chapter.Id,
            LocationValue = "kobo.stale",
            LocationType = "KoboSpan",
            LocationSource = "OEBPS/epub.xhtml",
        });
        await context.SaveChangesAsync();

        var mapper = Substitute.For<IKoboLocationMapper>();
        mapper.TryMapBookScrollIdToLocationAsync(
                Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((KoboMappedLocation?)null);
        mapper.TryMapLocationToBookScrollIdAsync(
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var rematch = new KoboLocationRematchService(unitOfWork, mapper,
            Substitute.For<ILogger<KoboLocationRematchService>>());
        await rematch.RematchAfterDeviceFileChangeAsync(chapter.Id, "/cache/book.kepub.epub");

        Assert.Empty(context.AppUserKoboReadingLocation);

        var progress = Assert.Single(context.AppUserProgresses);
        Assert.Equal(7, progress.PagesRead);
        Assert.Equal("//body/p[2]", progress.BookScrollId);
    }

    [Fact]
    public async Task Rematch_KeepsLocation_WhenStillValidInNewFile()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (user, chapter) = await SeedChapter(unitOfWork, context);

        context.AppUserProgresses.Add(new AppUserProgress
        {
            AppUserId = user.Id,
            ChapterId = chapter.Id,
            VolumeId = chapter.VolumeId,
            SeriesId = chapter.Volume.SeriesId,
            LibraryId = chapter.Volume.Series.LibraryId,
            PagesRead = 3,
            BookScrollId = "//body/p[1]",
        });
        context.AppUserKoboReadingLocation.Add(new AppUserKoboReadingLocation
        {
            AppUserId = user.Id,
            ChapterId = chapter.Id,
            LocationValue = "kobo.1.1",
            LocationType = "KoboSpan",
            LocationSource = "OEBPS/ch.xhtml",
        });
        await context.SaveChangesAsync();

        var mapper = Substitute.For<IKoboLocationMapper>();
        mapper.TryMapBookScrollIdToLocationAsync(
                Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((KoboMappedLocation?)null);
        mapper.TryMapLocationToBookScrollIdAsync(
                "/cache/book.kepub.epub", "kobo.1.1", "KoboSpan", "OEBPS/ch.xhtml",
                Arg.Any<CancellationToken>())
            .Returns("id(\"kobo.1.1\")");

        var rematch = new KoboLocationRematchService(unitOfWork, mapper,
            Substitute.For<ILogger<KoboLocationRematchService>>());
        await rematch.RematchAfterDeviceFileChangeAsync(chapter.Id, "/cache/book.kepub.epub");

        var location = Assert.Single(context.AppUserKoboReadingLocation);
        Assert.Equal("kobo.1.1", location.LocationValue);
        Assert.Equal(3, Assert.Single(context.AppUserProgresses).PagesRead);
    }

    [Fact]
    public async Task Rematch_ConvertChapter_UpsertsLocationFromPagesRead_WhenKepubTrusted()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (user, chapter) = await SeedConvertChapter(unitOfWork, context);

        context.AppUserProgresses.Add(new AppUserProgress
        {
            AppUserId = user.Id,
            ChapterId = chapter.Id,
            VolumeId = chapter.VolumeId,
            SeriesId = chapter.Volume.SeriesId,
            LibraryId = chapter.Volume.Series.LibraryId,
            PagesRead = 4,
        });
        context.AppUserKoboReadingLocation.Add(new AppUserKoboReadingLocation
        {
            AppUserId = user.Id,
            ChapterId = chapter.Id,
            LocationValue = "stale",
            LocationType = "KoboSpan",
            LocationSource = "OEBPS/Text/page_0001.xhtml",
        });
        await context.SaveChangesAsync();

        var kepubPath = Path.Join(Path.GetTempPath(),
            "kavita-rematch-convert-" + Guid.NewGuid().ToString("N") + ".kepub.epub");
        KoboConvertEpubTestFactory.WriteMinimalConvertEpub(kepubPath, 10);

        var rematch = new KoboLocationRematchService(unitOfWork, Substitute.For<IKoboLocationMapper>(),
            Substitute.For<ILogger<KoboLocationRematchService>>());
        await rematch.RematchAfterDeviceFileChangeAsync(chapter.Id, kepubPath);

        var location = Assert.Single(context.AppUserKoboReadingLocation);
        Assert.Equal(KoboConvertLocationCodec.ValueKoboSpan, location.LocationValue);
        Assert.Equal("OEBPS/Text/page_0005.xhtml", location.LocationSource);
        Assert.Equal(4, Assert.Single(context.AppUserProgresses).PagesRead);
        try { File.Delete(kepubPath); } catch { /* ignore */ }
    }

    [Fact]
    public async Task Rematch_ConvertChapter_ClearsLocation_WhenNewArtifactUntrusted()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (user, chapter) = await SeedConvertChapter(unitOfWork, context);

        context.AppUserProgresses.Add(new AppUserProgress
        {
            AppUserId = user.Id,
            ChapterId = chapter.Id,
            VolumeId = chapter.VolumeId,
            SeriesId = chapter.Volume.SeriesId,
            LibraryId = chapter.Volume.Series.LibraryId,
            PagesRead = 4,
        });
        context.AppUserKoboReadingLocation.Add(new AppUserKoboReadingLocation
        {
            AppUserId = user.Id,
            ChapterId = chapter.Id,
            LocationValue = KoboConvertLocationCodec.ValueKoboSpan,
            LocationType = KoboConvertLocationCodec.TypeKoboSpan,
            LocationSource = "OEBPS/Text/page_0005.xhtml",
        });
        await context.SaveChangesAsync();

        var badPath = Path.Join(Path.GetTempPath(),
            "kavita-rematch-bad-" + Guid.NewGuid().ToString("N") + ".kepub.epub");
        KoboConvertEpubTestFactory.WriteMinimalConvertEpub(badPath, 3); // ≠ chapter.Pages (10)

        var rematch = new KoboLocationRematchService(unitOfWork, Substitute.For<IKoboLocationMapper>(),
            Substitute.For<ILogger<KoboLocationRematchService>>());
        await rematch.RematchAfterDeviceFileChangeAsync(chapter.Id, badPath);

        var location = Assert.Single(context.AppUserKoboReadingLocation);
        Assert.Null(location.LocationValue);
        Assert.Equal(4, Assert.Single(context.AppUserProgresses).PagesRead);
        try { File.Delete(badPath); } catch { /* ignore */ }
    }

    private static async Task<(AppUser User, Chapter Chapter)> SeedChapter(IUnitOfWork unitOfWork,
        DataContext context)
    {
        var library = new LibraryBuilder("Rematch Lib").WithAllowKoboSync(true).Build();
        unitOfWork.LibraryRepository.Add(library);
        await unitOfWork.CommitAsync();

        context.AppUser.Add(new AppUserBuilder("rematch", "rematch@localhost")
            .WithLibrary(library)
            .WithLocale("en")
            .WithRole(PolicyConstants.LoginRole)
            .Build());
        await context.SaveChangesAsync();
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.None);
        Assert.NotNull(user);

        var chapter = new ChapterBuilder("1").WithPages(10).Build();
        var series = new SeriesBuilder("Rematch Book")
            .WithFormat(MangaFormat.Epub)
            .WithVolume(new VolumeBuilder("0").WithChapter(chapter).Build())
            .Build();
        series.Library = library;
        context.Series.Add(series);
        await context.SaveChangesAsync();

        chapter = await context.Chapter.Include(c => c.Volume).ThenInclude(v => v.Series)
            .Include(c => c.Files)
            .SingleAsync(c => c.Id == chapter.Id);
        return (user, chapter);
    }

    private static async Task<(AppUser User, Chapter Chapter)> SeedConvertChapter(IUnitOfWork unitOfWork,
        DataContext context)
    {
        var library = new LibraryBuilder("Rematch Convert Lib").WithAllowKoboSync(true).Build();
        unitOfWork.LibraryRepository.Add(library);
        await unitOfWork.CommitAsync();

        context.AppUser.Add(new AppUserBuilder("rematch-convert", "rematch-convert@localhost")
            .WithLibrary(library)
            .WithLocale("en")
            .WithRole(PolicyConstants.LoginRole)
            .Build());
        await context.SaveChangesAsync();
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(
            await context.AppUser.Where(u => u.UserName == "rematch-convert").Select(u => u.Id)
                .SingleAsync(),
            AppUserIncludes.None);
        Assert.NotNull(user);

        var cbzPath = Path.GetFullPath(Path.Join(Directory.GetCurrentDirectory(),
            "../../../Test Data/ArchiveService/CoverImages/v10.cbz"));
        var chapter = new ChapterBuilder("1")
            .WithPages(10)
            .WithFile(new MangaFileBuilder(cbzPath, MangaFormat.Archive, 10).WithExtension(".cbz").Build())
            .Build();
        var series = new SeriesBuilder("Rematch Convert Comic")
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder("0").WithChapter(chapter).Build())
            .Build();
        series.Library = library;
        context.Series.Add(series);
        await context.SaveChangesAsync();

        chapter = await context.Chapter.Include(c => c.Volume).ThenInclude(v => v.Series)
            .Include(c => c.Files)
            .SingleAsync(c => c.Id == chapter.Id);
        return (user, chapter);
    }
}
