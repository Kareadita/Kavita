using Kavita.Models.Entities.Enums;
using Kavita.Models.Images;
using Kavita.Services.Images;
using Microsoft.Extensions.Logging;
using NetVips;
using NSubstitute;

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
        var fileName = ImageService.GetWebLinkFormat("https://anilist.co", EncodeFormat.JPEG);

        _pipeline.Write(new ImageSource.FromBytes(TransparentPng()), ImageKind.Favicon,
            Policy with { Format = EncodeFormat.JPEG }, _directory, fileName);

        Assert.Equal("anilist.co.png", fileName);
        using var written = Image.NewFromFile(Path.Join(_directory, fileName));
        Assert.Equal("pngload", written.Get("vips-loader"));
        Assert.True(written.HasAlpha());
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
