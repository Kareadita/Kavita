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
using Kavita.Models.Images;
using Kavita.Services.Builders;
using Kavita.Services.Helpers;
using Kavita.Services.Images;
using Kavita.Services.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Services.Tests;

[Collection(NetVipsGlobalStateCollection.Name)]
public class MetadataServiceTests(ITestOutputHelper outputHelper) : AbstractDbTest(outputHelper)
{
    private static readonly DateTime FileTime = new(2024, 1, 1, 12, 0, 0);

    /// <summary>
    /// Real files in a temp folder: the cover check reads the source file's write time from disk
    /// </summary>
    private sealed class Harness(
        IUnitOfWork unitOfWork,
        DataContext context,
        MetadataService service,
        IReadingItemService readingItemService,
        IMediaErrorService mediaErrorService,
        ServerSettingDto settings,
        string root) : IDisposable
    {
        private readonly int _maxFiles = NetVips.Cache.MaxFiles;

        public IUnitOfWork UnitOfWork { get; } = unitOfWork;
        public DataContext Context { get; } = context;
        public MetadataService Service { get; } = service;
        public IReadingItemService ReadingItemService { get; } = readingItemService;
        public IMediaErrorService MediaErrorService { get; } = mediaErrorService;
        public ServerSettingDto Settings { get; } = settings;
        public string Covers { get; } = Path.Join(root, "covers");

        /// <summary>
        /// The image every file's cover is read from
        /// </summary>
        public string SourceImage { get; set; } = WriteImage(Path.Join(root, "source.png"));

        public MangaFile File(string name)
        {
            var path = Path.Join(root, "files", name);
            System.IO.File.WriteAllBytes(path, [1]);
            System.IO.File.SetLastWriteTime(path, FileTime);
            return new MangaFileBuilder(path, MangaFormat.Archive).WithLastModified(FileTime).Build();
        }

        public async Task Generate(int seriesId, bool forceUpdate = false, bool forceColorScape = false)
        {
            Context.ChangeTracker.Clear();
            await Service.GenerateCoversForSeries(Settings, 1, seriesId, forceUpdate, forceColorScape);
        }

        public async Task<Series> Load(int seriesId)
        {
            Context.ChangeTracker.Clear();
            return await Context.Series
                .Include(s => s.CoverImageFile)
                .Include(s => s.Volumes)
                .ThenInclude(v => v.CoverImageFile)
                .Include(s => s.Volumes)
                .ThenInclude(v => v.Chapters)
                .ThenInclude(c => c.CoverImageFile)
                .AsSplitQuery()
                .SingleAsync(s => s.Id == seriesId);
        }

        public void ClearCalls()
        {
            ReadingItemService.ClearReceivedCalls();
        }

        public void Dispose()
        {
            NetVips.Cache.MaxFiles = _maxFiles;
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    private async Task<Harness> Setup()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        // Matches Program.cs, otherwise libvips' operation cache keeps every file open and Windows refuses the delete
        NetVips.Cache.MaxFiles = 0;
        var root = Path.Join(Path.GetTempPath(), "kavita-metadata-" + Guid.NewGuid().ToString("N"));
        var covers = Path.Join(root, "covers");
        Directory.CreateDirectory(covers);
        Directory.CreateDirectory(Path.Join(root, "files"));

        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.FileSystem.Returns(new FileSystem());
        directoryService.CoverImageDirectory.Returns(covers);

        Harness? harness = null;
        var readingItemService = Substitute.For<IReadingItemService>();
        readingItemService.OpenCover(default!, default).ReturnsForAnyArgs(_ =>
            new OpenedCover(new ImageSource.FromFile(harness!.SourceImage)));

        var mediaErrorService = Substitute.For<IMediaErrorService>();
        var imageFileService = new ImageFileService(unitOfWork, new ImagePipeline(Substitute.For<ILogger<ImagePipeline>>()),
            directoryService, Substitute.For<IEventHub>(), Substitute.For<ILogger<ImageFileService>>());

        var service = new MetadataService(Substitute.For<IServiceScopeFactory>(), unitOfWork,
            Substitute.For<ILogger<MetadataService>>(), Substitute.For<IEventHub>(),
            new CacheHelper(new FileService()), readingItemService, directoryService, imageFileService, mediaErrorService);
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();

        harness = new Harness(unitOfWork, context, service, readingItemService, mediaErrorService, settings, root);
        return harness;
    }

    private static string WriteImage(string path)
    {
        using var black = NetVips.Image.Black(64, 91, bands: 3);
        using var left = black + new double[] { 200, 80, 40 };
        using var rightBlack = NetVips.Image.Black(32, 91, bands: 3);
        using var right = rightBlack + new double[] { 30, 60, 200 };
        using var colored = left.Insert(right, 32, 0);
        using var cast = colored.Cast(NetVips.Enums.BandFormat.Uchar);
        cast.WriteToFile(path);
        return path;
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

        h.ReadingItemService.DidNotReceiveWithAnyArgs().OpenCover(default!, default);
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
        SetCustomCover(h, series, ImageKind.SeriesCover);
        series.CoverImageLocked = true;
        SetCustomCover(h, series.Volumes[0], ImageKind.VolumeCover);
        series.Volumes[0].CoverImageLocked = true;
        await h.Context.SaveChangesAsync();
        await h.Generate(seriesId);
        h.ClearCalls();

        await h.Generate(seriesId);

        var after = await h.Load(seriesId);
        Assert.StartsWith("custom", after.CoverImage);
        Assert.StartsWith("custom", after.Volumes[0].CoverImage);
        Assert.Equal(CustomColor, after.CoverImageFile!.PrimaryColor);
        Assert.Equal(CustomColor, after.Volumes[0].CoverImageFile!.PrimaryColor);
    }

    [Fact]
    public async Task GenerateCoversForSeries_SourceReplacedWithOlderTime_CoverRegenerated()
    {
        using var h = await Setup();
        var file = h.File("v1.cbz");
        var seriesId = await AddSeries(h, new VolumeBuilder("1").WithChapter(new ChapterBuilder("1").WithFile(file).Build()).Build());
        await h.Generate(seriesId);
        var olderTime = new DateTime(2020, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(file.FilePath, olderTime);
        h.ClearCalls();

        await h.Generate(seriesId);

        h.ReadingItemService.ReceivedWithAnyArgs(1).OpenCover(default!, default);
        var chapter = (await h.Load(seriesId)).Volumes[0].Chapters[0];
        Assert.Equal(olderTime, chapter.CoverFileWriteTimeUtc);
    }

    [Fact]
    public async Task GenerateCoversForSeries_NoStoredSourceTime_RegeneratesOnceThenQuiet()
    {
        using var h = await Setup();
        var file = h.File("v1.cbz");
        var seriesId = await AddSeries(h, new VolumeBuilder("1").WithChapter(new ChapterBuilder("1").WithFile(file).Build()).Build());
        await h.Generate(seriesId);
        await h.Context.Chapter.ExecuteUpdateAsync(s => s.SetProperty(c => c.CoverFileWriteTimeUtc, (DateTime?) null));
        h.ClearCalls();

        await h.Generate(seriesId);
        await h.Generate(seriesId);

        h.ReadingItemService.ReceivedWithAnyArgs(1).OpenCover(default!, default);
        var chapter = (await h.Load(seriesId)).Volumes[0].Chapters[0];
        Assert.Equal(File.GetLastWriteTimeUtc(file.FilePath), chapter.CoverFileWriteTimeUtc);
    }

    [Fact]
    public async Task GenerateCoversForSeries_NoStoredSourceTime_SourceReplacedWithOlderTime_CoverRegenerated()
    {
        using var h = await Setup();
        var file = h.File("v1.cbz");
        var seriesId = await AddSeries(h, new VolumeBuilder("1").WithChapter(new ChapterBuilder("1").WithFile(file).Build()).Build());
        await h.Generate(seriesId);
        await h.Context.Chapter.ExecuteUpdateAsync(s => s.SetProperty(c => c.CoverFileWriteTimeUtc, (DateTime?) null));
        await File.WriteAllBytesAsync(file.FilePath, [1, 2, 3]);
        File.SetLastWriteTimeUtc(file.FilePath, new DateTime(2020, 6, 1, 8, 0, 0, DateTimeKind.Utc));
        h.ClearCalls();

        await h.Generate(seriesId);

        h.ReadingItemService.ReceivedWithAnyArgs(1).OpenCover(default!, default);
    }

    [Fact]
    public async Task CoverBackfillThenWordCountRecountInSameScope_KeepsCoverStamp()
    {
        using var h = await Setup();
        var file = h.File("v1.cbz");
        var seriesId = await AddSeries(h, new VolumeBuilder("1").WithChapter(new ChapterBuilder("1").WithFile(file).Build()).Build());
        await h.Generate(seriesId);
        await h.Context.Chapter.ExecuteUpdateAsync(s => s.SetProperty(c => c.CoverFileWriteTimeUtc, (DateTime?) null));
        var wordCount = new Kavita.Services.Metadata.WordCountAnalyzerService(
            Substitute.For<ILogger<Kavita.Services.Metadata.WordCountAnalyzerService>>(), h.UnitOfWork,
            Substitute.For<IEventHub>(), Substitute.For<IMediaErrorService>(),
            new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new FileSystem()));

        h.Context.ChangeTracker.Clear();
        await h.Service.GenerateCoversForSeries(h.Settings, 1, seriesId, forceUpdate: false, forceColorScape: false);
        await wordCount.ScanSeries(1, seriesId, forceUpdate: false);

        Assert.NotNull((await h.Load(seriesId)).Volumes[0].Chapters[0].CoverFileWriteTimeUtc);
    }

    [Fact]
    public async Task GenerateCoversForSeries_LockedChapterCover_SourceChanged_KeptUntilUnlocked()
    {
        using var h = await Setup();
        var file = h.File("v1.cbz");
        var seriesId = await AddSeries(h, new VolumeBuilder("1").WithChapter(new ChapterBuilder("1").WithFile(file).Build()).Build());
        await h.Generate(seriesId);

        var series = await h.Load(seriesId);
        var stamp = series.Volumes[0].Chapters[0].CoverFileWriteTimeUtc;
        // An upload rewrites the chapter's own row
        series.Volumes[0].Chapters[0].CoverImageFile!.Origin = ImageOrigin.Upload;
        series.Volumes[0].Chapters[0].CoverImageLocked = true;
        await h.Context.SaveChangesAsync();
        File.SetLastWriteTimeUtc(file.FilePath, new DateTime(2020, 6, 1, 8, 0, 0, DateTimeKind.Utc));
        h.ClearCalls();

        await h.Generate(seriesId);

        h.ReadingItemService.DidNotReceiveWithAnyArgs().OpenCover(default!, default);
        var locked = (await h.Load(seriesId)).Volumes[0].Chapters[0];
        Assert.Equal(ImageOrigin.Upload, locked.CoverImageFile!.Origin);
        Assert.Equal(stamp, locked.CoverFileWriteTimeUtc);

        locked.CoverImageLocked = false;
        await h.Context.SaveChangesAsync();
        await h.Generate(seriesId);

        h.ReadingItemService.ReceivedWithAnyArgs(1).OpenCover(default!, default);
    }

    [Fact]
    public async Task GenerateCovers_NewChapter_VolumeAndSeriesShareRow()
    {
        using var h = await Setup();
        var seriesId = await AddSeries(h, new VolumeBuilder("1").WithChapter(Chapter(h, "1", "v1.cbz")).Build());

        await h.Generate(seriesId);

        var series = await h.Load(seriesId);
        var chapter = series.Volumes[0].Chapters[0];
        Assert.NotNull(chapter.CoverImageFileId);
        Assert.Equal(ImageOrigin.Scan, chapter.CoverImageFile!.Origin);
        Assert.Equal(chapter.CoverImageFileId, series.Volumes[0].CoverImageFileId);
        Assert.Equal(chapter.CoverImageFileId, series.CoverImageFileId);
    }

    [Fact]
    public async Task GenerateCovers_MissingFile_RebuiltIntoSameRow()
    {
        using var h = await Setup();
        var seriesId = await AddSeries(h, new VolumeBuilder("1").WithChapter(Chapter(h, "1", "v1.cbz")).Build());
        await h.Generate(seriesId);
        var row = (await h.Load(seriesId)).Volumes[0].Chapters[0].CoverImageFile!;
        var path = Path.Join(h.Covers, row.FileName);
        File.Delete(path);

        await h.Generate(seriesId);

        var chapter = (await h.Load(seriesId)).Volumes[0].Chapters[0];
        Assert.Equal(row.Id, chapter.CoverImageFileId);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task Scan_LockedCoverFileMissing_RebuiltUnlocked()
    {
        using var h = await Setup();
        var seriesId = await AddSeries(h,
            new VolumeBuilder("1").WithChapter(Chapter(h, "1", "v1.cbz")).Build(),
            new VolumeBuilder("2").WithChapter(Chapter(h, "1", "v2.cbz")).Build());
        await h.Generate(seriesId);

        // After a restore: K+ chapter cover shared by its locked volume, an uploaded volume cover and K+ series cover, all gone
        var series = await h.Load(seriesId);
        var volume1 = series.Volumes.Single(v => v.Name == "1");
        var volume2 = series.Volumes.Single(v => v.Name == "2");
        var chapter1 = volume1.Chapters[0];
        chapter1.CoverImageFile!.Origin = ImageOrigin.KavitaPlus;
        chapter1.CoverImageLocked = true;
        volume1.CoverImageLocked = true;
        File.Delete(Path.Join(h.Covers, chapter1.CoverImageFile.FileName));
        volume2.CoverImageFile = new ImageFile { Kind = ImageKind.VolumeCover, FileName = "volume_upload.webp", Origin = ImageOrigin.Upload };
        volume2.CoverImageLocked = true;
        series.CoverImageFile = new ImageFile { Kind = ImageKind.SeriesCover, FileName = "series_kplus.webp", Origin = ImageOrigin.KavitaPlus };
        series.CoverImageLocked = true;
        await h.Context.SaveChangesAsync();

        await h.Generate(seriesId);

        series = await h.Load(seriesId);
        volume1 = series.Volumes.Single(v => v.Name == "1");
        volume2 = series.Volumes.Single(v => v.Name == "2");
        chapter1 = volume1.Chapters[0];
        var chapter2 = volume2.Chapters[0];

        Assert.False(chapter1.CoverImageLocked);
        Assert.Equal(ImageOrigin.Scan, chapter1.CoverImageFile!.Origin);
        Assert.True(File.Exists(Path.Join(h.Covers, chapter1.CoverImageFile.FileName)));

        Assert.False(volume1.CoverImageLocked);
        Assert.Equal(chapter1.CoverImageFileId, volume1.CoverImageFileId);
        Assert.False(volume2.CoverImageLocked);
        Assert.Equal(chapter2.CoverImageFileId, volume2.CoverImageFileId);

        Assert.False(series.CoverImageLocked);
        Assert.Equal(chapter1.CoverImageFileId, series.CoverImageFileId);
        Assert.Equal(ImageOrigin.Scan, series.CoverImageFile!.Origin);
        Assert.False(await h.Context.ImageFile.AnyAsync(f => f.FileName == "volume_upload.webp" || f.FileName == "series_kplus.webp"));
    }

    [Fact]
    public async Task GenerateCovers_UnreadableCover_ClearsChapterVolumeSeries()
    {
        using var h = await Setup();
        var seriesId = await AddSeries(h, new VolumeBuilder("1").WithChapter(Chapter(h, "1", "v1.cbz")).Build());
        await h.Generate(seriesId);
        var row = (await h.Load(seriesId)).Volumes[0].Chapters[0].CoverImageFile!;
        Assert.NotNull(row);

        h.SourceImage = Path.Join(h.Covers, "..", "garbage.png");
        await File.WriteAllBytesAsync(h.SourceImage, [1, 2, 3, 4]);
        await h.Generate(seriesId, forceUpdate: true);

        var series = await h.Load(seriesId);
        Assert.Null(series.Volumes[0].Chapters[0].CoverImageFileId);
        Assert.Null(series.Volumes[0].CoverImageFileId);
        Assert.Null(series.CoverImageFileId);
        Assert.False(File.Exists(Path.Join(h.Covers, row.FileName)));
        await h.MediaErrorService.ReceivedWithAnyArgs(1)
            .ReportMediaIssueAsync(default!, default, MediaErrorReason.CoverFailed, default(Exception)!);
    }

    [Fact]
    public async Task GenerateCovers_FormatChanged_OldFileDeleted()
    {
        using var h = await Setup();
        await SetEncodeFormat(h, EncodeFormat.WEBP);
        var seriesId = await AddSeries(h, new VolumeBuilder("1").WithChapter(Chapter(h, "1", "v1.cbz")).Build());
        await h.Generate(seriesId);
        var webp = (await h.Load(seriesId)).Volumes[0].Chapters[0].CoverImageFile!;
        Assert.EndsWith(".webp", webp.FileName);

        await SetEncodeFormat(h, EncodeFormat.PNG);
        await h.Generate(seriesId, forceUpdate: true);

        var row = (await h.Load(seriesId)).Volumes[0].Chapters[0].CoverImageFile!;
        Assert.Equal(webp.Id, row.Id);
        Assert.EndsWith(".png", row.FileName);
        Assert.True(File.Exists(Path.Join(h.Covers, row.FileName)));
        Assert.False(File.Exists(Path.Join(h.Covers, webp.FileName)));
    }

    [Fact]
    public async Task GenerateCovers_ColorsOnly_WriteRowColors()
    {
        using var h = await Setup();
        var seriesId = await AddSeries(h, new VolumeBuilder("1").WithChapter(Chapter(h, "1", "v1.cbz")).Build());
        await h.Generate(seriesId);
        await h.Context.ImageFile.ExecuteUpdateAsync(s => s
            .SetProperty(f => f.PrimaryColor, (string?) null)
            .SetProperty(f => f.SecondaryColor, (string?) null));
        h.ClearCalls();

        await h.Generate(seriesId);

        h.ReadingItemService.DidNotReceiveWithAnyArgs().OpenCover(default!, default);
        var row = (await h.Load(seriesId)).Volumes[0].Chapters[0].CoverImageFile!;
        Assert.False(string.IsNullOrEmpty(row.PrimaryColor));
        Assert.False(string.IsNullOrEmpty(row.SecondaryColor));
    }

    private static async Task SetEncodeFormat(Harness h, EncodeFormat format)
    {
        await h.Context.ServerSetting.Where(s => s.Key == ServerSettingKey.EncodeMediaAs)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Value, format.ToString()));
    }

    private const string CustomColor = "#123456";

    private static void SetCustomCover(Harness h, IHasCoverImageFile entity, ImageKind kind)
    {
        var name = $"custom{Guid.NewGuid():N}.png";
        WriteImage(Path.Join(h.Covers, name));
        var row = new ImageFile
        {
            Kind = kind,
            FileName = name,
            Origin = ImageOrigin.Upload,
            Format = EncodeFormat.PNG,
            PrimaryColor = CustomColor,
            SecondaryColor = CustomColor,
        };
        h.Context.ImageFile.Add(row);
        entity.CoverImageFile = row;
        ((IHasCoverImage) entity).CoverImage = name;
    }
}
