using System.IO.Abstractions;
using Flurl.Http.Testing;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.Metadata;
using Kavita.API.Services.ReadingLists;
using Kavita.API.Services.SignalR;
using Kavita.Models.DTOs.Uploads;
using Kavita.Server.Controllers;
using Kavita.Services;
using Kavita.Services.Images;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Image = NetVips.Image;

namespace Kavita.Server.Tests.Controllers;

public class UploadControllerTests
{
    [Fact]
    public async Task GetImageFromFile_NamesFromHeader()
    {
        var directoryService = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new FileSystem());
        directoryService.ExistOrCreate(directoryService.TempDirectory);

        var controller = new UploadController(Substitute.For<IUnitOfWork>(),
            new ImageService(Substitute.For<ILogger<ImageService>>(), directoryService,
                new ImagePipeline(Substitute.For<ILogger<ImagePipeline>>())),
            Substitute.For<ILogger<UploadController>>(), Substitute.For<ITaskScheduler>(), directoryService,
            Substitute.For<IEventHub>(), Substitute.For<IReadingListService>(), Substitute.For<ILocalizationService>(),
            Substitute.For<ICoverDbService>(), Substitute.For<IUrlValidationService>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        using var png = Image.Black(64, 64, bands: 3);
        var bytes = png.PngsaveBuffer();
        using var httpTest = new HttpTest();

        httpTest.RespondWith(() => new ByteArrayContent(bytes));

        var result = await controller.GetImageFromFile(new UploadUrlDto { Url = "https://example.com/cover.svg" });

        var fileName = result.Value;
        try
        {
            Assert.NotNull(fileName);
            Assert.EndsWith(".png", fileName);
        }
        finally
        {
            if (fileName != null)
            {
                try { File.Delete(Path.Join(directoryService.TempDirectory, fileName)); } catch (IOException) { }
            }
        }
    }
}
