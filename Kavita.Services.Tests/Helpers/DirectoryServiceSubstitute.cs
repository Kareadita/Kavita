using System.IO.Abstractions;
using Kavita.API.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Kavita.Services.Tests.Helpers;

/// <summary>
/// An <see cref="IDirectoryService"/> for tests that write real image files: the folders point at the test's temp folders,
/// and the file calls the image code makes go to a real <see cref="DirectoryService"/> on disk
/// </summary>
public static class DirectoryServiceSubstitute
{
    public static IDirectoryService OnDisk(string covers, string? favicons = null, string? publishers = null)
    {
        var disk = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new FileSystem());
        var directoryService = Substitute.For<IDirectoryService>();

        directoryService.CoverImageDirectory.Returns(covers);
        if (favicons != null)
        {
            directoryService.FaviconDirectory.Returns(favicons);
        }
        if (publishers != null)
        {
            directoryService.PublisherDirectory.Returns(publishers);
        }

        directoryService.FileSystem.Returns(disk.FileSystem);
        directoryService.GetFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>())
            .Returns(c => disk.GetFiles(c.ArgAt<string>(0), c.ArgAt<string>(1), c.ArgAt<SearchOption>(2)).ToList());
        directoryService.DeleteFileQuietly(Arg.Any<string>())
            .Returns(c => disk.DeleteFileQuietly(c.ArgAt<string>(0)));

        return directoryService;
    }
}
