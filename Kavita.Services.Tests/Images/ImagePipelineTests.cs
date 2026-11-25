using System.IO.Compression;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Images;
using Kavita.Services.Images;
using Kavita.Services.Images.ImageSteps;
using Microsoft.Extensions.Logging;
using NetVips;
using NSubstitute;
using Kavita.Services.Helpers;

namespace Kavita.Services.Tests.Images;

public class ImagePipelineTests : IDisposable
{
    private readonly string _directory = Path.Join(Path.GetTempPath(), "kavita-pipeline-" + Guid.NewGuid().ToString("N"));
    private readonly ImagePipeline _pipeline = new(Substitute.For<ILogger<ImagePipeline>>());
    private static readonly ImagePolicy Policy = new(EncodeFormat.WEBP, CoverImageSize.Default);
    private static readonly string CoversDirectory =
        Path.GetFullPath(Path.Join(Directory.GetCurrentDirectory(), "../../../Test Data/ImageService/Covers"));
    private static readonly string ExpectedCoversDirectory =
        Path.GetFullPath(Path.Join(Directory.GetCurrentDirectory(), "../../../Test Data/ImageService/Covers/Expected"));

    public ImagePipelineTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void Write_DecodeFails_LeavesNoTmp()
    {
        var source = new ImageSource.FromBytes(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

        Assert.Throws<VipsException>(() =>
            _pipeline.Write(source, ImageKind.SeriesCover, Policy, _directory, "series1.webp"));

        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Theory]
    [InlineData("../series1.webp")]
    [InlineData("1/../../series1.webp")]
    [InlineData("..")]
    [InlineData("")]
    public void Write_NameOutsideFolder_ThrowsAndWritesNothing(string fileName)
    {
        var source = new ImageSource.FromBytes(TransparentPng());

        Assert.Throws<ArgumentException>(() =>
            _pipeline.Write(source, ImageKind.SeriesCover, Policy, _directory, fileName));
        Assert.Throws<ArgumentException>(() =>
            _pipeline.Reencode(TransparentPng(), ImageKind.SeriesCover, Policy, _directory, fileName));

        Assert.Empty(Directory.GetFileSystemEntries(Path.GetDirectoryName(_directory)!, Path.GetFileName(_directory) + "*")
            .Where(p => p != _directory));
        Assert.Empty(Directory.GetFileSystemEntries(_directory));
    }

    [Fact]
    public void Write_Jpeg_TransparencyBecomesWhite()
    {
        var source = new ImageSource.FromBytes(TransparentPng());

        _pipeline.Write(source, ImageKind.SeriesCover, Policy with { Format = EncodeFormat.JPEG }, _directory, "series1.jpg");

        using var written = Image.NewFromFile(Path.Join(_directory, "series1.jpg"));
        Assert.True(written.Getpoint(0, 0)[0] > 250);
    }

    [Fact]
    public void Write_JpegSetting_FaviconWrittenAndLookedUpAsPng()
    {
        var fileName = ImageHelper.GetWebLinkFormat("https://anilist.co", EncodeFormat.JPEG);

        _pipeline.Write(new ImageSource.FromBytes(TransparentPng()), ImageKind.Favicon,
            Policy with { Format = EncodeFormat.JPEG }, _directory, fileName);

        Assert.Equal("anilist.co.png", fileName);
        using var written = Image.NewFromFile(Path.Join(_directory, fileName));
        Assert.Equal("pngload", written.Get("vips-loader"));
        Assert.True(written.HasAlpha());
    }

    [Fact]
    public void Reencode_SmallCover_NotEnlarged()
    {
        var source = Path.Join(_directory, "series1.webp");
        using (var image = Image.Black(320, 455, bands: 3))
        {
            image.Webpsave(source);
        }

        var result = _pipeline.Reencode(source, ImageKind.SeriesCover,
            new ImagePolicy(EncodeFormat.PNG, CoverImageSize.Medium), _directory, "series1.png");

        using var written = Image.NewFromFile(Path.Join(_directory, "series1.png"));
        Assert.Equal("pngload", written.Get("vips-loader"));
        Assert.Equal((320, 455), (written.Width, written.Height));
        Assert.Equal((320, 455), (result.Width, result.Height));
    }

    /// <summary>
    /// Archive covers reach Write as a zip entry stream, which cannot seek; most scan covers take this path
    /// </summary>
    [Fact]
    public void Write_ZipEntryStream_FramedAsBefore()
    {
        using var zip = new MemoryStream();
        using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, leaveOpen: true))
        {
            archive.CreateEntryFromFile(Path.Join(CoversDirectory, "comic-normal.jpg"), "001.jpg");
        }

        zip.Position = 0;
        using (var archive = new ZipArchive(zip, ZipArchiveMode.Read))
        using (var entry = archive.Entries[0].Open())
        {
            Assert.False(entry.CanSeek);
            _pipeline.Write(new ImageSource.FromStream(entry), ImageKind.ChapterCover, Policy, _directory, "comic-normal.webp");
        }

        using var written = Image.NewFromFile(Path.Join(_directory, "comic-normal.webp"));
        using var expected = Image.NewFromFile(Path.Join(ExpectedCoversDirectory, "comic-normal.expected.webp"));
        Assert.Equal((expected.Width, expected.Height), (written.Width, written.Height));
        using var difference = (written - expected).Abs();
        Assert.True(difference.Avg() < 1, $"mean pixel difference {difference.Avg():F2}");
    }

    [Fact]
    public void Render_PngWithAlpha_FormatIsUsedAsIs()
    {
        var bytes = _pipeline.Render(new ImageSource.FromBytes(TransparentPng()), new ImageRender(EncodeFormat.PNG, []));

        using var rendered = Image.NewFromBuffer(bytes);
        Assert.Equal("pngload_buffer", rendered.Get("vips-loader"));
        Assert.True(rendered.HasAlpha());
    }

    [Fact]
    public void Run_ResizeNotFirst_StillResizes()
    {
        var (width, height, bands) = RunOnBlack(640, 909, [new GreyscaleStep(), new FitWithinStep(355, 530)], out _);

        Assert.Equal((355, 504, 1), (width, height, bands));
    }

    [Fact]
    public void Run_NoSteps_KeepsSourceSize()
    {
        var (width, height, bands) = RunOnBlack(640, 909, [], out var staged);

        Assert.Equal((640, 909, 3), (width, height, bands));
        Assert.Equal((640, 909, 640, 909), (staged.Width, staged.Height, staged.SourceWidth, staged.SourceHeight));
    }

    private static (int Width, int Height, int Bands) RunOnBlack(int width, int height, IReadOnlyList<IImageStep> steps,
        out ImagePipeline.StagedImage staged)
    {
        byte[] png;
        using (var black = Image.Black(width, height, bands: 3))
        {
            png = black.PngsaveBuffer();
        }

        var encoded = (0, 0, 0);
        staged = ImagePipeline.Run(new ImageSource.FromBytes(png), steps, img =>
        {
            using var decoded = Image.NewFromBuffer(img.PngsaveBuffer());
            encoded = (decoded.Width, decoded.Height, decoded.Bands);
        });

        return encoded;
    }

    private static byte[] TransparentPng()
    {
        using var image = Image.Black(8, 8, bands: 4);
        return image.PngsaveBuffer();
    }

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); } catch (IOException) { }
    }
}
