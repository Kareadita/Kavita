using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.ReadingLists;
using Kavita.API.Services.SignalR;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.DTOs.Uploads;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Images;
using Kavita.Server.Controllers;
using Kavita.Services.Builders;
using Kavita.Services.Images;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Server.Tests.Controllers;

public class UploadControllerTests(ITestOutputHelper outputHelper) : AbstractDbTest(outputHelper)
{
    [Fact]
    public async Task ResetChapter_VolumeOnOwnRow_Untouched()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var chapter1 = new ChapterBuilder("1").Build();
        var chapter2 = new ChapterBuilder("2").Build();
        var volume = new VolumeBuilder("1").WithChapter(chapter1).WithChapter(chapter2).Build();
        var series = new SeriesBuilder("Pandora Hearts").WithVolume(volume).Build();
        series.LibraryId = (await context.Library.FirstAsync()).Id;
        context.Series.Add(series);
        await context.SaveChangesAsync();

        var volumeRow = new ImageFile { Kind = ImageKind.VolumeCover, FileName = $"v{volume.Id}.webp", Origin = ImageOrigin.Upload };
        var chapterRow = new ImageFile { Kind = ImageKind.ChapterCover, FileName = $"v{volume.Id}_c{chapter2.Id}.webp", Origin = ImageOrigin.Upload };
        context.ImageFile.AddRange(volumeRow, chapterRow);
        await context.SaveChangesAsync();
        volume.CoverImageFileId = volumeRow.Id;
        volume.CoverImageLocked = true;
        chapter2.CoverImageFileId = chapterRow.Id;
        chapter2.CoverImageLocked = true;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await CreateController(unitOfWork).UploadChapterCoverImageFromUrl(new UploadCoverFileDto { Id = chapter2.Id });

        Assert.IsType<OkResult>(result);
        context.ChangeTracker.Clear();
        Assert.Null((await context.Chapter.SingleAsync(c => c.Id == chapter2.Id)).CoverImageFileId);

        var savedVolume = await context.Volume.SingleAsync(v => v.Id == volume.Id);
        Assert.Equal(volumeRow.Id, savedVolume.CoverImageFileId);
        Assert.True(savedVolume.CoverImageLocked);
        Assert.True(await context.ImageFile.AnyAsync(f => f.Id == volumeRow.Id));
    }

    private static UploadController CreateController(IUnitOfWork unitOfWork)
    {
        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.CoverImageDirectory.Returns(Path.Join(Path.GetTempPath(), "kavita-upload-tests"));

        var imageFileService = new ImageFileService(unitOfWork, Substitute.For<IImagePipeline>(), directoryService,
            Substitute.For<IEventHub>(), Substitute.For<ILogger<ImageFileService>>());

        return new UploadController(unitOfWork, Substitute.For<ILogger<UploadController>>(),
            Substitute.For<ITaskScheduler>(), directoryService, Substitute.For<IReadingListService>(),
            Substitute.For<ILocalizationService>(), Substitute.For<IUrlValidationService>(), imageFileService,
            Substitute.For<ICompositeCoverService>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }
}
