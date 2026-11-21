using Kavita.Models.Entities.Enums;
using Kavita.Models.Images;
using Kavita.Services.Images;
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
