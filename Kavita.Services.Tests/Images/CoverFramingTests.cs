using Kavita.Models.Entities.Enums;
using Kavita.Models.Extensions;
using Kavita.Models.Images;
using Kavita.Services.Images;
using Microsoft.Extensions.Logging;
using NetVips;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Services.Tests.Images;

/// <summary>
/// How Write frames each cover shape (wide, square, webtoon, spread) as a scan cover. To look at it, uncomment the
/// [Fact] on <see cref="GenerateGallery"/>, run it, then open the framing.html it prints
/// </summary>
public class CoverFramingTests(ITestOutputHelper output) : AbstractImageTest, IDisposable
{
    private readonly ImagePipeline _pipeline = new(Substitute.For<ILogger<ImagePipeline>>());
    private readonly string _directory = Path.Join(Path.GetTempPath(), "kavita-framing-" + Guid.NewGuid().ToString("N"));
    private static readonly ImagePolicy Policy = new(EncodeFormat.WEBP, CoverImageSize.Default);
    private static readonly string ExpectedDirectory = Path.Join(CoversDirectory, "Expected");

    protected override string OutputDirectory { get; } = Path.Join(ImageServiceDirectory, "Framing");

    public static TheoryData<string> CoverSamples()
    {
        var data = new TheoryData<string>();
        foreach (var sample in GetSamples(CoversDirectory))
        {
            data.Add(Path.GetFileName(sample));
        }
        return data;
    }

    /// <summary>
    /// Expected files are the Write output from before the step engine (WebP, Default cover size); a crop or size
    /// change shows up here
    /// </summary>
    [Theory]
    [MemberData(nameof(CoverSamples))]
    public void Write_Covers_FramedAsBefore(string sample)
    {
        var written = WriteCover(Path.Join(CoversDirectory, sample), _directory, Path.GetFileNameWithoutExtension(sample) + ".webp");

        Assert.Null(CompareWithExpected(sample, written));
    }

    // [Fact]
    public void GenerateGallery()
    {
        ClearOutputs();
        var (boxWidth, boxHeight) = Policy.CoverSize.GetDimensions();

        var rows = new List<GalleryRow>();
        foreach (var sample in GetSamples(CoversDirectory))
        {
            var name = Path.GetFileName(sample);
            var outputPath = WriteCover(sample, OutputDirectory, Path.GetFileName(OutputPath(sample, "write", ".webp")));

            var difference = CompareWithExpected(name, outputPath);
            rows.Add(new GalleryRow(name,
            [
                Cell("Source", sample, Framing(sample, boxWidth, boxHeight)),
                Cell("Expected", ExpectedPath(name), boxWidth: boxWidth, boxHeight: boxHeight),
                Cell("Write", outputPath, difference ?? "pass", boxWidth, boxHeight),
            ]));
        }

        output.WriteLine(WriteGallery("framing.html", "Cover framing", rows));
    }

    private string WriteCover(string source, string directory, string fileName)
    {
        Directory.CreateDirectory(directory);
        _pipeline.Write(new ImageSource.FromFile(source), ImageKind.ChapterCover, Policy, directory, fileName);
        return Path.Join(directory, fileName);
    }

    /// <returns>Null when the written cover matches its expected file, else what differs</returns>
    private static string? CompareWithExpected(string sample, string writtenPath)
    {
        using var written = Image.NewFromFile(writtenPath);
        using var expected = Image.NewFromFile(ExpectedPath(sample));
        if ((expected.Width, expected.Height, expected.Bands) != (written.Width, written.Height, written.Bands))
        {
            return $"FAIL: {written.Width}x{written.Height}x{written.Bands}, expected {expected.Width}x{expected.Height}x{expected.Bands}";
        }

        using var difference = (written - expected).Abs();
        var mean = difference.Avg();
        return mean < 1 ? null : $"FAIL: mean pixel difference {mean:F2}";
    }

    private static string ExpectedPath(string sample) =>
        Path.Join(ExpectedDirectory, Path.GetFileNameWithoutExtension(sample) + ".expected.webp");

    private static string Framing(string sample, int boxWidth, int boxHeight)
    {
        using var image = Image.NewFromFile(sample);
        var size = ImagePipeline.GetSizeForDimensions(image.Width, image.Height, boxWidth, boxHeight);
        var crop = ImagePipeline.GetCropForDimensions(image.Width, image.Height, boxWidth, boxHeight);
        var scalesWell = ImagePipeline.WillScaleWell(image.Width, image.Height, boxWidth, boxHeight);
        return $"ratio {(double) image.Width / image.Height:F2}, size {size}, crop {crop?.ToString() ?? "none"}, scales well {scalesWell}";
    }

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); } catch (IOException) { }
    }
}
