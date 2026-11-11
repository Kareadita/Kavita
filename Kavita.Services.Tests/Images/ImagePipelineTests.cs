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

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); } catch (IOException) { }
    }
}
