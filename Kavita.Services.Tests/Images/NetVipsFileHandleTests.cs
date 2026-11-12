using Kavita.Common.Extensions;
using Kavita.Models.Entities.Enums;
using Kavita.Services.Tests.Helpers;
using Image = NetVips.Image;

namespace Kavita.Services.Tests.Images;

/// <summary>
/// Windows refuses to replace a file that a live NetVips image still maps ("user-mapped section open"),
/// so every reader must dispose each intermediate image before it returns
/// </summary>
[Collection(NetVipsGlobalStateCollection.Name)]
public class NetVipsFileHandleTests : IDisposable
{
    private readonly string _directory = Path.Join(Path.GetTempPath(), "kavita-vips-handle-" + Guid.NewGuid().ToString("N"));
    private readonly int _maxFiles = NetVips.Cache.MaxFiles;

    public NetVipsFileHandleTests()
    {
        // Matches Program.cs, otherwise libvips' operation cache holds every file a test opened
        NetVips.Cache.MaxFiles = 0;
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        NetVips.Cache.MaxFiles = _maxFiles;
        try { Directory.Delete(_directory, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void GetBetterImage_FullDecode_LeavesBothFilesReplaceable()
    {
        if (!OperatingSystem.IsWindows()) return;

        // Under 3x the pixels of each other, so both are fully decoded, and over 512px so both get resized
        var existing = WriteWebp("existing.webp", 640, 909);
        var incoming = WriteWebp("incoming.webp", 700, 1000);

        existing.GetBetterImage(incoming);

        AssertReplaceable(existing);
        AssertReplaceable(incoming);
    }

    [Fact]
    public void CalculateSimilarity_DifferentSizes_LeavesBothFilesReplaceable()
    {
        if (!OperatingSystem.IsWindows()) return;

        var existing = WriteWebp("existing.webp", 640, 909);
        var incoming = WriteWebp("incoming.webp", 700, 1000);

        existing.CalculateSimilarity(incoming);

        AssertReplaceable(existing);
        AssertReplaceable(incoming);
    }

    [Fact]
    public void CalculateColorScape_LeavesFileReplaceable()
    {
        if (!OperatingSystem.IsWindows()) return;

        var cover = WriteWebp("cover.webp", 640, 909);

        ImageService.CalculateColorScape(cover);

        AssertReplaceable(cover);
    }

    [Fact]
    public void CreateMergedImage_LeavesSourcesReplaceable()
    {
        if (!OperatingSystem.IsWindows()) return;

        var first = WriteWebp("first.webp", 640, 909);
        var second = WriteWebp("second.webp", 640, 909);

        ImageService.CreateMergedImage([first, second], CoverImageSize.Default, Path.Join(_directory, "merged.webp"));

        AssertReplaceable(first);
        AssertReplaceable(second);
    }

    private string WriteWebp(string fileName, int width, int height)
    {
        var path = Path.Join(_directory, fileName);
        using var black = Image.Black(width, height, bands: 3);
        using var image = black + 128;
        image.Webpsave(path);
        return path;
    }

    private static void AssertReplaceable(string path)
    {
        var replacement = path + ".tmp";
        File.WriteAllBytes(replacement, [1, 2, 3]);

        var error = Record.Exception(() => File.Move(replacement, path, overwrite: true));

        Assert.Null(error);
    }
}
