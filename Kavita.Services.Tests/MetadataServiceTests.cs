using System.IO.Abstractions;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Database;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.DTOs.Settings;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Interfaces;
using Kavita.Services.Builders;
using Kavita.Services.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Services.Tests;

public class MetadataServiceTests(ITestOutputHelper outputHelper) : AbstractDbTest(outputHelper)
{
    private static readonly DateTime FileTime = new(2024, 1, 1, 12, 0, 0);

    /// <summary>
    /// Real files in a temp folder: MangaFile.UpdateLastModified reads System.IO directly, so a mock file system
    /// would make every chapter look modified on the second pass
    /// </summary>
    private sealed class Harness(
        IUnitOfWork unitOfWork,
        DataContext context,
        MetadataService service,
        IImageService imageService,
        IReadingItemService readingItemService,
        ServerSettingDto settings,
        string root) : IDisposable
    {
        public IUnitOfWork UnitOfWork { get; } = unitOfWork;
        public DataContext Context { get; } = context;
        public MetadataService Service { get; } = service;
        public IImageService ImageService { get; } = imageService;
        public IReadingItemService ReadingItemService { get; } = readingItemService;
        public ServerSettingDto Settings { get; } = settings;
        public string Covers { get; } = Path.Join(root, "covers");

        public MangaFile File(string name)
        {
            var path = Path.Join(root, "files", name);
            System.IO.File.WriteAllBytes(path, [1]);
            System.IO.File.SetLastWriteTime(path, FileTime);
            return new MangaFileBuilder(path, MangaFormat.Archive).WithLastModified(FileTime).Build();
        }

        public async Task Generate(int seriesId)
        {
            Context.ChangeTracker.Clear();
            await Service.GenerateCoversForSeries(Settings, 1, seriesId, forceUpdate: false, forceColorScape: false);
        }

        public async Task<Series> Load(int seriesId)
        {
            Context.ChangeTracker.Clear();
            return await Context.Series
                .Include(s => s.Volumes)
                .ThenInclude(v => v.Chapters)
                .SingleAsync(s => s.Id == seriesId);
        }

        public void ClearCalls()
        {
            ImageService.ClearReceivedCalls();
            ReadingItemService.ClearReceivedCalls();
        }

        public void Dispose()
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    private async Task<Harness> Setup()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var root = Path.Join(Path.GetTempPath(), "kavita-metadata-" + Guid.NewGuid().ToString("N"));
        var covers = Path.Join(root, "covers");
        Directory.CreateDirectory(covers);
        Directory.CreateDirectory(Path.Join(root, "files"));

        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.FileSystem.Returns(new FileSystem());
        directoryService.CoverImageDirectory.Returns(covers);

        var readingItemService = Substitute.For<IReadingItemService>();
        readingItemService.GetCoverImage(default!, default!, default, default, default).ReturnsForAnyArgs(ci =>
        {
            var name = ci.ArgAt<string>(1) + ".png";
            File.WriteAllBytes(Path.Join(covers, name), [1]);
            return name;
        });

        var imageService = Substitute.For<IImageService>();
        imageService.When(x => x.UpdateColorScape(Arg.Any<IHasCoverImage>())).Do(ci =>
        {
            var entity = ci.Arg<IHasCoverImage>();
            entity.PrimaryColor = "#111111";
            entity.SecondaryColor = "#222222";
        });

        var service = new MetadataService(Substitute.For<IServiceScopeFactory>(), unitOfWork,
            Substitute.For<ILogger<MetadataService>>(), Substitute.For<IEventHub>(),
            new CacheHelper(new FileService()), readingItemService, directoryService, imageService);
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();

        return new Harness(unitOfWork, context, service, imageService, readingItemService, settings, root);
    }

    private static async Task<int> AddSeries(Harness h, params Volume[] volumes)
    {
        var series = new SeriesBuilder("Accel World").WithLibraryId(1).WithVolumes(volumes.ToList()).Build();
        h.Context.Series.Add(series);
        await h.Context.SaveChangesAsync();
        return series.Id;
    }

    private static Chapter Chapter(Harness h, string number, string file) =>
        new ChapterBuilder(number).WithFile(h.File(file)).Build();

    [Fact]
    public async Task GenerateCoversForSeries_SecondPassNothingChanged_NoColorScapeAndNoWrites()
    {
        using var h = await Setup();
        var seriesId = await AddSeries(h,
            new VolumeBuilder("1").WithChapter(Chapter(h, "1", "v1.cbz")).Build(),
            new VolumeBuilder("2").WithChapter(Chapter(h, "2", "v2.cbz")).Build());

        await h.Generate(seriesId);
        var before = await h.Load(seriesId);
        var volumesModified = before.Volumes.ToDictionary(v => v.Id, v => v.LastModifiedUtc);
        var seriesModified = before.LastModifiedUtc;
        h.ClearCalls();

        await h.Generate(seriesId);

        h.ImageService.DidNotReceiveWithAnyArgs().UpdateColorScape(default!);
        h.ReadingItemService.DidNotReceiveWithAnyArgs().GetCoverImage(default!, default!, default, default, default);
        var after = await h.Load(seriesId);
        Assert.All(after.Volumes, v => Assert.Equal(volumesModified[v.Id], v.LastModifiedUtc));
        Assert.Equal(seriesModified, after.LastModifiedUtc);
    }

    [Fact]
    public async Task GenerateCoversForSeries_ChapterOneAddedAfterChapterTwo_VolumeCoverMovesToChapterOne()
    {
        using var h = await Setup();
        var seriesId = await AddSeries(h, new VolumeBuilder("1").WithChapter(Chapter(h, "2", "c2.cbz")).Build());
        await h.Generate(seriesId);

        var series = await h.Load(seriesId);
        series.Volumes[0].Chapters.Add(Chapter(h, "1", "c1.cbz"));
        await h.Context.SaveChangesAsync();

        await h.Generate(seriesId);

        var volume = (await h.Load(seriesId)).Volumes[0];
        var chapterOne = volume.Chapters.Single(c => c.MinNumber == 1);
        Assert.NotNull(chapterOne.CoverImage);
        Assert.Equal(chapterOne.CoverImage, volume.CoverImage);
    }

    [Fact]
    public async Task GenerateCoversForSeries_FirstChapterRemoved_VolumeCoverMovesToNextChapter()
    {
        using var h = await Setup();
        var seriesId = await AddSeries(h, new VolumeBuilder("1")
            .WithChapter(Chapter(h, "1", "c1.cbz"))
            .WithChapter(Chapter(h, "2", "c2.cbz"))
            .Build());
        await h.Generate(seriesId);

        var series = await h.Load(seriesId);
        h.Context.Chapter.Remove(series.Volumes[0].Chapters.Single(c => c.MinNumber == 1));
        await h.Context.SaveChangesAsync();

        await h.Generate(seriesId);

        var volume = (await h.Load(seriesId)).Volumes[0];
        Assert.Equal(volume.Chapters.Single().CoverImage, volume.CoverImage);
    }

    [Fact]
    public async Task GenerateCoversForSeries_FirstVolumeRemoved_SeriesCoverMovesToNextVolume()
    {
        using var h = await Setup();
        var seriesId = await AddSeries(h,
            new VolumeBuilder("1").WithChapter(Chapter(h, "1", "v1.cbz")).Build(),
            new VolumeBuilder("2").WithChapter(Chapter(h, "2", "v2.cbz")).Build());
        await h.Generate(seriesId);

        var series = await h.Load(seriesId);
        h.Context.Volume.Remove(series.Volumes.Single(v => v.MinNumber == 1));
        await h.Context.SaveChangesAsync();

        await h.Generate(seriesId);

        var after = await h.Load(seriesId);
        Assert.Equal(after.Volumes.Single().CoverImage, after.CoverImage);
    }

    [Fact]
    public async Task GenerateCoversForSeries_LockedCustomCovers_NotRecomputedOnSecondPass()
    {
        using var h = await Setup();
        var seriesId = await AddSeries(h, new VolumeBuilder("1").WithChapter(Chapter(h, "1", "v1.cbz")).Build());
        await h.Generate(seriesId);

        var series = await h.Load(seriesId);
        series.CoverImage = AddCustomCover(h);
        series.CoverImageLocked = true;
        series.Volumes[0].CoverImage = AddCustomCover(h);
        series.Volumes[0].CoverImageLocked = true;
        await h.Context.SaveChangesAsync();
        await h.Generate(seriesId);
        h.ClearCalls();

        await h.Generate(seriesId);

        h.ImageService.DidNotReceiveWithAnyArgs().UpdateColorScape(default!);
        var after = await h.Load(seriesId);
        Assert.StartsWith("custom", after.CoverImage);
        Assert.StartsWith("custom", after.Volumes[0].CoverImage);
    }

    private static string AddCustomCover(Harness h)
    {
        var name = $"custom{Guid.NewGuid():N}.png";
        File.WriteAllBytes(Path.Join(h.Covers, name), [1]);
        return name;
    }
}
