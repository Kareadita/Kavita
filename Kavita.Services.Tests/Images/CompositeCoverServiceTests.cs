using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Database;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.User;
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
public class CompositeCoverServiceTests : AbstractDbTest, IDisposable
{
    private readonly string _covers = Path.Join(Path.GetTempPath(), "kavita-composite-" + Guid.NewGuid().ToString("N"));
    private readonly int _maxFiles = NetVips.Cache.MaxFiles;
    private int _imagesWritten;

    public CompositeCoverServiceTests(ITestOutputHelper output) : base(output)
    {
        // Matches Program.cs, otherwise libvips' operation cache keeps every file open and Windows refuses the delete
        NetVips.Cache.MaxFiles = 0;
        Directory.CreateDirectory(_covers);
    }

    public void Dispose()
    {
        NetVips.Cache.MaxFiles = _maxFiles;
        try { Directory.Delete(_covers, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public async Task Refresh_CollectionWithItems_SetsCover()
    {
        var (service, context) = await Setup();
        var collection = await SeedCollection(context, seriesCount: 4);

        await service.RefreshAsync(ImageKind.CollectionCover, collection.Id, force: false);

        context.ChangeTracker.Clear();
        var saved = await context.AppUserCollection.Include(c => c.CoverImageFile).SingleAsync(c => c.Id == collection.Id);
        Assert.NotNull(saved.CoverImageFile);
        Assert.Equal(ImageKind.CollectionCover, saved.CoverImageFile.Kind);
        Assert.Equal(ImageOrigin.Composite, saved.CoverImageFile.Origin);
        Assert.False(string.IsNullOrEmpty(saved.CoverImageFile.InputHash));
        Assert.True(File.Exists(Path.Join(_covers, saved.CoverImageFile.FileName)));
        Assert.False(saved.CoverImageLocked);
    }

    [Fact]
    public async Task Refresh_UnchangedInputs_Skips()
    {
        var (service, context) = await Setup();
        var collection = await SeedCollection(context, seriesCount: 4);
        await service.RefreshAsync(ImageKind.CollectionCover, collection.Id, force: false);
        context.ChangeTracker.Clear();
        var row = (await context.AppUserCollection.Include(c => c.CoverImageFile)
            .SingleAsync(c => c.Id == collection.Id)).CoverImageFile!;
        var coverPath = Path.Join(_covers, row.FileName);

        // A skipped refresh does not write, so the deleted file stays gone
        File.Delete(coverPath);
        await service.RefreshAsync(ImageKind.CollectionCover, collection.Id, force: false);
        Assert.False(File.Exists(coverPath));

        await service.RefreshAsync(ImageKind.CollectionCover, collection.Id, force: true);
        Assert.True(File.Exists(coverPath));

        File.Delete(coverPath);
        context.ChangeTracker.Clear();
        var tracked = await context.AppUserCollection.Include(c => c.Items).SingleAsync(c => c.Id == collection.Id);
        tracked.Items.Add(await AddSeriesWithCover(context, "Added Later"));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await service.RefreshAsync(ImageKind.CollectionCover, collection.Id, force: false);
        Assert.True(File.Exists(coverPath));
    }

    [Fact]
    public async Task Refresh_UploadedOrLocked_DoesNothing()
    {
        var (service, context) = await Setup();

        var uploaded = await SeedCollection(context, seriesCount: 4);
        var uploadRow = new ImageFile
        {
            Kind = ImageKind.CollectionCover,
            FileName = $"tag{uploaded.Id}.webp",
            Origin = ImageOrigin.Upload,
            Format = EncodeFormat.WEBP,
        };
        WriteImage(Path.Join(_covers, uploadRow.FileName), 320, 455);
        context.ImageFile.Add(uploadRow);
        uploaded.CoverImageFile = uploadRow;
        await context.SaveChangesAsync();

        var locked = await SeedCollection(context, seriesCount: 4);
        locked.CoverImageLocked = true;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await service.RefreshAsync(ImageKind.CollectionCover, uploaded.Id, force: true);
        await service.RefreshAsync(ImageKind.CollectionCover, locked.Id, force: true);

        context.ChangeTracker.Clear();
        var savedUpload = await context.AppUserCollection.Include(c => c.CoverImageFile).SingleAsync(c => c.Id == uploaded.Id);
        Assert.Equal(uploadRow.Id, savedUpload.CoverImageFileId);
        Assert.Equal(ImageOrigin.Upload, savedUpload.CoverImageFile!.Origin);
        Assert.Null(savedUpload.CoverImageFile.InputHash);
        var savedLocked = await context.AppUserCollection.SingleAsync(c => c.Id == locked.Id);
        Assert.Null(savedLocked.CoverImageFileId);
    }

    [Fact]
    public async Task Refresh_CoverWithAlpha_SetsCover()
    {
        var (service, context) = await Setup();
        var collection = await SeedCollection(context, seriesCount: 2);
        var withAlpha = await context.AppUserCollection.Where(c => c.Id == collection.Id)
            .SelectMany(c => c.Items.Select(s => s.CoverImageFile!.FileName)).FirstAsync();
        WriteImage(Path.Join(_covers, withAlpha), 320, 455, bands: 4);
        context.ChangeTracker.Clear();

        await service.RefreshAsync(ImageKind.CollectionCover, collection.Id, force: false);

        context.ChangeTracker.Clear();
        Assert.NotNull((await context.AppUserCollection.SingleAsync(c => c.Id == collection.Id)).CoverImageFileId);
    }

    [Fact]
    public async Task RefreshAll_EmptiedCollection_ClearsBuiltCover()
    {
        var (service, context) = await Setup();
        var collection = await SeedCollection(context, seriesCount: 2);
        await service.RefreshAsync(ImageKind.CollectionCover, collection.Id, force: false);

        context.ChangeTracker.Clear();
        var tracked = await context.AppUserCollection.Include(c => c.Items).SingleAsync(c => c.Id == collection.Id);
        tracked.Items.Clear();
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await service.CreateMissingCoversAsync();
        context.ChangeTracker.Clear();
        Assert.NotNull((await context.AppUserCollection.SingleAsync(c => c.Id == collection.Id)).CoverImageFileId);

        await service.RefreshAllAsync();
        context.ChangeTracker.Clear();
        Assert.Null((await context.AppUserCollection.SingleAsync(c => c.Id == collection.Id)).CoverImageFileId);
    }

    private async Task<(ICompositeCoverService Service, DataContext Context)> Setup()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.CoverImageDirectory.Returns(_covers);
        var pipeline = new ImagePipeline(Substitute.For<ILogger<ImagePipeline>>());
        var imageFileService = new ImageFileService(unitOfWork, pipeline, directoryService, Substitute.For<IEventHub>(),
            Substitute.For<ILogger<ImageFileService>>());
        var service = new CompositeCoverService(unitOfWork, imageFileService, directoryService,
            Substitute.For<ILogger<CompositeCoverService>>());

        return (service, context);
    }

    private async Task<AppUserCollection> SeedCollection(DataContext context, int seriesCount)
    {
        var user = await context.AppUser.FirstOrDefaultAsync();
        if (user == null)
        {
            user = new AppUserBuilder("collector", "collector@kavita.test").Build();
            context.AppUser.Add(user);
            await context.SaveChangesAsync();
        }

        var series = new List<Series>();
        for (var i = 0; i < seriesCount; i++)
        {
            series.Add(await AddSeriesWithCover(context, $"Series {Guid.NewGuid():N}"));
        }

        var collection = new AppUserCollectionBuilder("Collection " + Guid.NewGuid().ToString("N")).WithItems(series).Build();
        collection.AppUserId = user.Id;
        context.AppUserCollection.Add(collection);
        await context.SaveChangesAsync();
        return collection;
    }

    private async Task<Series> AddSeriesWithCover(DataContext context, string name)
    {
        var series = new SeriesBuilder(name).Build();
        series.LibraryId = (await context.Library.FirstAsync()).Id;
        context.Series.Add(series);
        await context.SaveChangesAsync();

        var row = new ImageFile
        {
            Kind = ImageKind.SeriesCover,
            FileName = $"series{series.Id}.webp",
            Origin = ImageOrigin.Scan,
            Format = EncodeFormat.WEBP,
        };
        WriteImage(Path.Join(_covers, row.FileName), 320, 455);
        context.ImageFile.Add(row);
        series.CoverImageFile = row;
        await context.SaveChangesAsync();
        return series;
    }

    private void WriteImage(string path, int width, int height, int bands = 3)
    {
        _imagesWritten++;
        using var black = Image.Black(width, height, bands: bands);
        using var colored = black + new double[] { 200, 80, _imagesWritten * 40 % 256, 128 }.Take(bands).ToArray();
        using var cast = colored.Cast(NetVips.Enums.BandFormat.Uchar);
        cast.Webpsave(path);
    }
}
