using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Database;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.DTOs.SignalR.Bodies;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Interfaces;
using Kavita.Models.Images;
using Kavita.Services.Builders;
using Kavita.Services.Images;
using Kavita.Services.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;
using Image = NetVips.Image;

namespace Kavita.Services.Tests.Images;

[Collection(NetVipsGlobalStateCollection.Name)]
public class ImageFileServiceTests : AbstractDbTest, IDisposable
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47];

    private readonly string _covers = Path.Join(Path.GetTempPath(), "kavita-imagefile-" + Guid.NewGuid().ToString("N"));
    private readonly string _incoming;
    private readonly int _maxFiles = NetVips.Cache.MaxFiles;
    private readonly IEventHub _eventHub = Substitute.For<IEventHub>();
    private int _imagesWritten;

    public ImageFileServiceTests(ITestOutputHelper output) : base(output)
    {
        _incoming = Path.Join(_covers, "incoming");
        // Matches Program.cs, otherwise libvips' operation cache keeps every file open and Windows refuses the delete
        NetVips.Cache.MaxFiles = 0;
        Directory.CreateDirectory(_incoming);
    }

    public void Dispose()
    {
        NetVips.Cache.MaxFiles = _maxFiles;
        try { Directory.Delete(_covers, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public async Task SetCover_PngSetting_NameMatchesBytes()
    {
        var (service, _, context) = await Setup(EncodeFormat.PNG);
        var series = await SeedSeries(context);
        var row = await AddRow(context, ImageKind.SeriesCover, $"series{series.Id}.webp");
        Link(series, row);
        await context.SaveChangesAsync();

        await service.SetCoverAsync(series, Incoming("kplus.webp", 600, 900), new CoverWrite(ImageOrigin.KavitaPlus, Lock: false));

        context.ChangeTracker.Clear();
        var updated = await context.ImageFile.SingleAsync(f => f.Id == row.Id);
        Assert.Equal($"series{series.Id}.png", updated.FileName);
        Assert.Equal(EncodeFormat.PNG, updated.Format);
        Assert.Equal(PngSignature, File.ReadAllBytes(Path.Join(_covers, updated.FileName)).Take(4));
        Assert.False(File.Exists(Path.Join(_covers, $"series{series.Id}.webp")));
        var saved = await context.Series.SingleAsync(s => s.Id == series.Id);
        Assert.Equal(row.Id, saved.CoverImageFileId);
    }

    [Fact]
    public async Task SetCover_SharedChapterRow_SharersFollow()
    {
        var (service, _, context) = await Setup(EncodeFormat.WEBP);
        var series = await SeedSeries(context);
        var volume = series.Volumes[0];
        var chapter = volume.Chapters[0];
        var row = await AddRow(context, ImageKind.ChapterCover, $"v{volume.Id}_c{chapter.Id}.webp");
        Link(chapter, row);
        Link(volume, row);
        Link(series, row);
        await context.SaveChangesAsync();
        var before = File.ReadAllBytes(Path.Join(_covers, row.FileName));

        await service.SetCoverAsync(chapter, Incoming("rescan.png", 700, 1000), new CoverWrite(ImageOrigin.Scan, Lock: false));

        context.ChangeTracker.Clear();
        var updated = await context.ImageFile.SingleAsync(f => f.Id == row.Id);
        Assert.NotEqual(before, File.ReadAllBytes(Path.Join(_covers, updated.FileName)));
        var savedChapter = await context.Chapter.SingleAsync(c => c.Id == chapter.Id);
        var savedVolume = await context.Volume.SingleAsync(v => v.Id == volume.Id);
        var savedSeries = await context.Series.SingleAsync(s => s.Id == series.Id);
        Assert.Equal(row.Id, savedChapter.CoverImageFileId);
        Assert.Equal(row.Id, savedVolume.CoverImageFileId);
        Assert.Equal(row.Id, savedSeries.CoverImageFileId);

        await AssertCoverUpdateSent(chapter.Id, MessageFactoryEntityTypes.Chapter);
        await AssertCoverUpdateSent(volume.Id, MessageFactoryEntityTypes.Volume);
        await AssertCoverUpdateSent(series.Id, MessageFactoryEntityTypes.Series);
    }

    [Fact]
    public async Task SetCover_NoCommit_NoEvent()
    {
        var (service, _, context) = await Setup(EncodeFormat.WEBP);
        var series = await SeedSeries(context);

        await service.SetCoverAsync(series, Incoming("upload.png", 600, 900), new CoverWrite(ImageOrigin.Upload, Lock: true),
            commit: false);

        await _eventHub.DidNotReceiveWithAnyArgs().SendMessageAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task SetCover_VolumeOnChapterRow_GetsOwnRow()
    {
        var (service, _, context) = await Setup(EncodeFormat.WEBP);
        var series = await SeedSeries(context);
        var volume = series.Volumes[0];
        var chapter = volume.Chapters[0];
        var chapterRow = await AddRow(context, ImageKind.ChapterCover, $"v{volume.Id}_c{chapter.Id}.webp");
        Link(chapter, chapterRow);
        Link(volume, chapterRow);
        await context.SaveChangesAsync();
        var chapterBytes = File.ReadAllBytes(Path.Join(_covers, chapterRow.FileName));

        await service.SetCoverAsync(volume, Incoming("upload.png", 600, 900), new CoverWrite(ImageOrigin.Upload, Lock: true));

        context.ChangeTracker.Clear();
        var savedVolume = await context.Volume.SingleAsync(v => v.Id == volume.Id);
        Assert.NotEqual(chapterRow.Id, savedVolume.CoverImageFileId);
        var volumeRow = await context.ImageFile.SingleAsync(f => f.Id == savedVolume.CoverImageFileId);
        Assert.Equal(ImageKind.VolumeCover, volumeRow.Kind);
        Assert.Equal($"v{volume.Id}.webp", volumeRow.FileName);
        var savedChapterRow = await context.ImageFile.SingleAsync(f => f.Id == chapterRow.Id);
        Assert.Equal(chapterRow.FileName, savedChapterRow.FileName);
        Assert.Equal(chapterBytes, File.ReadAllBytes(Path.Join(_covers, chapterRow.FileName)));
        Assert.Equal(chapterRow.Id, (await context.Chapter.SingleAsync(c => c.Id == chapter.Id)).CoverImageFileId);
    }

    [Fact]
    public async Task ShareCover_LockBoth_LocksChapter()
    {
        var (service, _, context) = await Setup(EncodeFormat.WEBP);
        var series = await SeedSeries(context);
        var volume = series.Volumes[0];
        var chapter = volume.Chapters[0];
        var row = await AddRow(context, ImageKind.ChapterCover, $"v{volume.Id}_c{chapter.Id}.webp");
        Link(chapter, row);
        await context.SaveChangesAsync();

        await service.ShareCoverAsync(volume, chapter, lockBoth: true);

        context.ChangeTracker.Clear();
        var savedVolume = await context.Volume.SingleAsync(v => v.Id == volume.Id);
        var savedChapter = await context.Chapter.SingleAsync(c => c.Id == chapter.Id);
        Assert.Equal(row.Id, savedVolume.CoverImageFileId);
        Assert.True(savedVolume.CoverImageLocked);
        Assert.True(savedChapter.CoverImageLocked);
    }

    [Fact]
    public async Task Unlock_OwnerWithLockedSharer_SharerIsUnshared()
    {
        // PNG setting with a WebP source: the copies must keep .webp, since their bytes are not re-encoded
        var (service, _, context) = await Setup(EncodeFormat.PNG);
        var series = await SeedSeries(context);
        var volume = series.Volumes[0];
        var chapter = volume.Chapters[0];
        var row = await AddRow(context, ImageKind.ChapterCover, $"v{volume.Id}_c{chapter.Id}.webp", ImageOrigin.KavitaPlus);
        Link(chapter, row);
        Link(volume, row);
        Link(series, row);
        chapter.CoverImageLocked = true;
        volume.CoverImageLocked = true;
        series.CoverImageLocked = true;
        await context.SaveChangesAsync();
        var sourceBytes = File.ReadAllBytes(Path.Join(_covers, row.FileName));

        await service.UpdateLockAsync(chapter, locked: false);

        context.ChangeTracker.Clear();
        var savedChapter = await context.Chapter.SingleAsync(c => c.Id == chapter.Id);
        Assert.False(savedChapter.CoverImageLocked);
        Assert.Equal(row.Id, savedChapter.CoverImageFileId);
        Assert.Equal(sourceBytes, File.ReadAllBytes(Path.Join(_covers, row.FileName)));

        var savedVolume = await context.Volume.SingleAsync(v => v.Id == volume.Id);
        Assert.True(savedVolume.CoverImageLocked);
        var volumeRow = await context.ImageFile.SingleAsync(f => f.Id == savedVolume.CoverImageFileId);
        Assert.Equal(ImageKind.VolumeCover, volumeRow.Kind);
        Assert.Equal($"v{volume.Id}.webp", volumeRow.FileName);
        Assert.Equal(ImageOrigin.KavitaPlus, volumeRow.Origin);
        Assert.Equal(sourceBytes, File.ReadAllBytes(Path.Join(_covers, volumeRow.FileName)));

        var savedSeries = await context.Series.SingleAsync(s => s.Id == series.Id);
        Assert.True(savedSeries.CoverImageLocked);
        var seriesRow = await context.ImageFile.SingleAsync(f => f.Id == savedSeries.CoverImageFileId);
        Assert.Equal(ImageKind.SeriesCover, seriesRow.Kind);
        Assert.Equal($"series{series.Id}.webp", seriesRow.FileName);
        Assert.Equal(sourceBytes, File.ReadAllBytes(Path.Join(_covers, seriesRow.FileName)));
    }

    [Fact]
    public async Task SetCover_UnlockWithLockedSharer_SharerKeepsOldArt()
    {
        var (service, _, context) = await Setup(EncodeFormat.WEBP);
        var series = await SeedSeries(context);
        var volume = series.Volumes[0];
        var chapter = volume.Chapters[0];
        var row = await AddRow(context, ImageKind.ChapterCover, $"v{volume.Id}_c{chapter.Id}.webp");
        Link(chapter, row);
        Link(volume, row);
        chapter.CoverImageLocked = true;
        volume.CoverImageLocked = true;
        await context.SaveChangesAsync();
        var oldBytes = File.ReadAllBytes(Path.Join(_covers, row.FileName));

        await service.SetCoverAsync(chapter, Incoming("upload.png", 600, 900), new CoverWrite(ImageOrigin.Upload, Lock: false));

        context.ChangeTracker.Clear();
        var savedVolume = await context.Volume.SingleAsync(v => v.Id == volume.Id);
        Assert.True(savedVolume.CoverImageLocked);
        Assert.NotEqual(row.Id, savedVolume.CoverImageFileId);
        var volumeRow = await context.ImageFile.SingleAsync(f => f.Id == savedVolume.CoverImageFileId);
        Assert.Equal($"v{volume.Id}.webp", volumeRow.FileName);
        Assert.Equal(oldBytes, File.ReadAllBytes(Path.Join(_covers, $"v{volume.Id}.webp")));
        Assert.NotEqual(oldBytes, File.ReadAllBytes(Path.Join(_covers, row.FileName)));
        Assert.False((await context.Chapter.SingleAsync(c => c.Id == chapter.Id)).CoverImageLocked);
    }

    [Fact]
    public async Task Unlock_Sharer_NoCopy()
    {
        var (service, _, context) = await Setup(EncodeFormat.WEBP);
        var series = await SeedSeries(context);
        var volume = series.Volumes[0];
        var chapter = volume.Chapters[0];
        var row = await AddRow(context, ImageKind.ChapterCover, $"v{volume.Id}_c{chapter.Id}.webp");
        Link(chapter, row);
        Link(volume, row);
        chapter.CoverImageLocked = true;
        volume.CoverImageLocked = true;
        await context.SaveChangesAsync();

        await service.UpdateLockAsync(volume, locked: false);

        context.ChangeTracker.Clear();
        Assert.Equal(1, await context.ImageFile.CountAsync());
        var savedVolume = await context.Volume.SingleAsync(v => v.Id == volume.Id);
        Assert.False(savedVolume.CoverImageLocked);
        Assert.Equal(row.Id, savedVolume.CoverImageFileId);
        Assert.True((await context.Chapter.SingleAsync(c => c.Id == chapter.Id)).CoverImageLocked);
        Assert.False(File.Exists(Path.Join(_covers, $"v{volume.Id}.webp")));
    }

    [Fact]
    public async Task SetCover_ChooseBetter_ExistingWins_NoWrite()
    {
        var (service, _, context) = await Setup(EncodeFormat.WEBP);
        var series = await SeedSeries(context);
        var chapter = series.Volumes[0].Chapters[0];
        var row = await AddRow(context, ImageKind.ChapterCover, $"v{chapter.VolumeId}_c{chapter.Id}.webp", ImageOrigin.Scan, 3000, 4300);
        Link(chapter, row);
        await context.SaveChangesAsync();
        var before = File.ReadAllBytes(Path.Join(_covers, row.FileName));

        await service.SetCoverAsync(chapter, Incoming("small.webp", 300, 400),
            new CoverWrite(ImageOrigin.KavitaPlus, Lock: false, ChooseBetter: true));

        context.ChangeTracker.Clear();
        var saved = await context.ImageFile.SingleAsync(f => f.Id == row.Id);
        Assert.Equal(ImageOrigin.Scan, saved.Origin);
        Assert.Equal(3000, saved.Width);
        Assert.Equal(before, File.ReadAllBytes(Path.Join(_covers, row.FileName)));
        Assert.Empty(Directory.GetFiles(_covers, "*.tmp"));
    }

    [Fact]
    public async Task ClearCover_SharedRow_KeepsRowForOthers()
    {
        var (service, _, context) = await Setup(EncodeFormat.WEBP);
        var series = await SeedSeries(context);
        var volume = series.Volumes[0];
        var chapter = volume.Chapters[0];
        var row = await AddRow(context, ImageKind.ChapterCover, $"v{volume.Id}_c{chapter.Id}.webp");
        Link(chapter, row);
        Link(volume, row);
        volume.CoverImageLocked = true;
        await context.SaveChangesAsync();

        await service.ClearCoverAsync(volume);

        context.ChangeTracker.Clear();
        var savedVolume = await context.Volume.SingleAsync(v => v.Id == volume.Id);
        Assert.Null(savedVolume.CoverImageFileId);
        Assert.False(savedVolume.CoverImageLocked);
        Assert.True(await context.ImageFile.AnyAsync(f => f.Id == row.Id));
        Assert.True(File.Exists(Path.Join(_covers, row.FileName)));
        Assert.Equal(row.Id, (await context.Chapter.SingleAsync(c => c.Id == chapter.Id)).CoverImageFileId);
    }

    [Fact]
    public async Task SetCover_RowWithSameNameExists_Adopts()
    {
        var (service, _, context) = await Setup(EncodeFormat.WEBP);
        var series = await SeedSeries(context);
        var chapter = series.Volumes[0].Chapters[0];
        // Chapter was reset while the series still held its row
        var row = await AddRow(context, ImageKind.ChapterCover, $"v{chapter.VolumeId}_c{chapter.Id}.webp");
        Link(series, row);
        await context.SaveChangesAsync();

        await service.SetCoverAsync(chapter, Incoming("rescan.png", 600, 900), new CoverWrite(ImageOrigin.Scan, Lock: false));

        context.ChangeTracker.Clear();
        Assert.Equal(1, await context.ImageFile.CountAsync());
        Assert.Equal(row.Id, (await context.Chapter.SingleAsync(c => c.Id == chapter.Id)).CoverImageFileId);
        Assert.Equal(row.Id, (await context.Series.SingleAsync(s => s.Id == series.Id)).CoverImageFileId);
    }

    [Theory]
    [InlineData("../escape.png")]
    [InlineData("DC/Vertigo.png")]
    public async Task WriteKeyed_NameWithFolder_Refused(string fileName)
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var publishers = Path.Join(_covers, "publishers");
        var directoryService = DirectoryServiceSubstitute.OnDisk(_covers, publishers: publishers);
        var pipeline = new ImagePipeline(Substitute.For<ILogger<ImagePipeline>>());
        var service = new ImageFileService(unitOfWork, pipeline, directoryService, _eventHub, Substitute.For<ILogger<ImageFileService>>());
        using var image = Image.Black(40, 30, bands: 3);

        await Assert.ThrowsAsync<ArgumentException>(() => service.WriteKeyedAsync(new ImageSource.FromBytes(image.PngsaveBuffer()),
            ImageKind.Publisher, "escape", fileName, ImageOrigin.CoversDb));

        Assert.False(File.Exists(Path.Join(_covers, "escape.png")));
        Assert.False(Directory.Exists(Path.Join(publishers, "DC")));
    }

    private async Task<(IImageFileService Service, IUnitOfWork UnitOfWork, DataContext Context)> Setup(EncodeFormat format)
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var setting = await context.ServerSetting.SingleAsync(s => s.Key == ServerSettingKey.EncodeMediaAs);
        setting.Value = format.ToString();
        await context.SaveChangesAsync();

        var directoryService = DirectoryServiceSubstitute.OnDisk(_covers);
        var pipeline = new ImagePipeline(Substitute.For<ILogger<ImagePipeline>>());
        var service = new ImageFileService(unitOfWork, pipeline, directoryService, _eventHub, Substitute.For<ILogger<ImageFileService>>());

        return (service, unitOfWork, context);
    }

    private Task AssertCoverUpdateSent(int id, string entityType) =>
        _eventHub.Received(1).SendMessageAsync(MessageFactory.CoverUpdate,
            Arg.Is<SignalRMessageDto>(m => Equals(m.Body, new CoverUpdateEventBodyDto(id, entityType))),
            false, Arg.Any<CancellationToken>());

    private static async Task<Series> SeedSeries(DataContext context)
    {
        var series = new SeriesBuilder("Pandora Hearts")
            .WithVolume(new VolumeBuilder("1").WithChapter(new ChapterBuilder("1").Build()).Build())
            .Build();
        var library = await context.Library.FirstAsync();
        series.LibraryId = library.Id;
        context.Series.Add(series);
        await context.SaveChangesAsync();
        return series;
    }

    private async Task<ImageFile> AddRow(DataContext context, ImageKind kind, string fileName,
        ImageOrigin origin = ImageOrigin.Scan, int width = 320, int height = 455)
    {
        WriteImage(Path.Join(_covers, fileName), width, height);
        var row = new ImageFile
        {
            Kind = kind,
            FileName = fileName,
            Origin = origin,
            Format = EncodeFormat.WEBP,
            Width = width,
            Height = height,
        };
        context.ImageFile.Add(row);
        await context.SaveChangesAsync();
        return row;
    }

    private ImageSource Incoming(string fileName, int width, int height)
    {
        var path = Path.Join(_incoming, fileName);
        WriteImage(path, width, height);
        return new ImageSource.FromFile(path);
    }

    private void WriteImage(string path, int width, int height)
    {
        // Each image gets its own color, so a replaced file is never byte-identical to the one before
        _imagesWritten++;
        using var black = Image.Black(width, height, bands: 3);
        using var colored = black + new double[] { 200, 80, _imagesWritten * 40 % 256 };
        using var cast = colored.Cast(NetVips.Enums.BandFormat.Uchar);
        if (path.EndsWith(".png"))
        {
            cast.Pngsave(path);
            return;
        }

        cast.Webpsave(path);
    }

    private static void Link(IHasCoverImageFile entity, ImageFile row)
    {
        entity.CoverImageFileId = row.Id;
        entity.CoverImageFile = row;
    }
}
