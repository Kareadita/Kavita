using Kavita.Models.Entities.Enums;
using Kavita.Models.Extensions;
using Kavita.Models.Images;
using Kavita.Services.Images;
using Kavita.Services.Images.ImageSteps;
using Kavita.Services.Kobo;
using Microsoft.Extensions.Logging;
using NetVips;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Services.Tests.Images;

/// <summary>
/// Each pipeline step on every sample in Covers/ and Transparency/. To look at the output, uncomment the [Fact] on
/// <see cref="GenerateGallery"/>, run it, then open the steps.html it prints
/// </summary>
public class ImageStepTests(ITestOutputHelper output) : AbstractImageTest
{
    private readonly ImagePipeline _pipeline = new(Substitute.For<ILogger<ImagePipeline>>());

    protected override string OutputDirectory { get; } = Path.Join(ImageServiceDirectory, "Steps");

    private static readonly StepCase[] Cases =
    [
        new("cover-thumbnail", "CoverThumbnailStep 320x455",
            new ImageRender(EncodeFormat.WEBP, [new CoverThumbnailStep(320, 455)]), 320, 455,
            (_, rendered) => Assert.Equal((320, 455), (rendered.Width, rendered.Height))),
        new("fit-within", "FitWithinStep 320x455",
            new ImageRender(EncodeFormat.WEBP, [new FitWithinStep(320, 455)]), 320, 455,
            (source, rendered) => AssertShrunkToFit(source, rendered, 320, 455)),
        new("greyscale", "GreyscaleStep (PNG)",
            new ImageRender(EncodeFormat.PNG, [new GreyscaleStep()]), null, null,
            (source, rendered) =>
            {
                Assert.Equal((source.Width, source.Height), (rendered.Width, rendered.Height));
                Assert.Equal(source.HasAlpha(), rendered.HasAlpha());
                AssertGrey(rendered);
            }),
        new("kobo-colour", "Kobo 355x530 colour",
            KoboSyncService.GetKoboRender("355", "530", null, false), 355, 530,
            (source, rendered) =>
            {
                AssertKoboJpeg(source, rendered);
                Assert.Equal(3, rendered.Bands);
            }),
        new("kobo-grey", "Kobo 355x530 greyscale",
            KoboSyncService.GetKoboRender("355", "530", null, true), 355, 530,
            (source, rendered) =>
            {
                AssertKoboJpeg(source, rendered);
                Assert.Equal(1, rendered.Bands);
            }),
    ];

    public static TheoryData<string, string> StepSamples()
    {
        var data = new TheoryData<string, string>();
        foreach (var sample in AllSamples())
        {
            foreach (var stepCase in Cases)
            {
                data.Add(stepCase.Name, Path.GetRelativePath(ImageServiceDirectory, sample).Replace('\\', '/'));
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(StepSamples))]
    public void Step_OnSample_KeepsItsRule(string caseName, string sample)
    {
        var stepCase = Cases.Single(c => c.Name == caseName);
        var path = Path.Join(ImageServiceDirectory, sample);

        var result = _pipeline.Render(new ImageSource.FromFile(path), stepCase.Render);

        using var source = Image.NewFromFile(path);
        using var rendered = Image.NewFromBuffer(result.Data);
        stepCase.Check(source, rendered);
        Assert.Equal((rendered.Width, rendered.Height), (result.Width, result.Height));
    }

    // [Fact]
    public void GenerateGallery()
    {
        ClearOutputs();

        var rows = new List<GalleryRow>();
        foreach (var sample in AllSamples())
        {
            var cells = new List<GalleryCell> { Cell("Source", sample) };
            foreach (var stepCase in Cases)
            {
                var outputPath = OutputPath(sample, stepCase.Name, stepCase.Render.Format.GetExtension());
                File.WriteAllBytes(outputPath, _pipeline.Render(new ImageSource.FromFile(sample), stepCase.Render).Data);
                cells.Add(Cell(stepCase.Title, outputPath, CheckResult(stepCase, sample, outputPath), stepCase.BoxWidth, stepCase.BoxHeight));
            }

            rows.Add(new GalleryRow(Path.GetRelativePath(ImageServiceDirectory, sample).Replace('\\', '/'), cells));
        }

        output.WriteLine(WriteGallery("steps.html", "Image pipeline steps", rows));
    }

    [Theory]
    [InlineData(600, 1200, Enums.Size.Both, Enums.Interesting.Attention)]
    [InlineData(2703, 1902, Enums.Size.Force, null)]
    [InlineData(640, 909, Enums.Size.Force, null)]
    public void CoverThumbnail_TargetFor_MatchesCoverFraming(int sourceWidth, int sourceHeight, Enums.Size size,
        Enums.Interesting? crop)
    {
        var target = new CoverThumbnailStep(320, 455).TargetFor(sourceWidth, sourceHeight);

        Assert.Equal((320, 455, size, crop), (target.Width, target.Height, target.Size, target.Crop));
    }

    [Theory]
    [InlineData(600, 1200)]
    [InlineData(100, 100)]
    public void FitWithin_TargetFor_IgnoresSourceAndNeverEnlarges(int sourceWidth, int sourceHeight)
    {
        var target = new FitWithinStep(320, 455).TargetFor(sourceWidth, sourceHeight);

        Assert.Equal((320, 455, Enums.Size.Down, (Enums.Interesting?) null), (target.Width, target.Height, target.Size, target.Crop));
    }

    /// <summary>
    /// Apply on a decoded image gives 320x455 for 640x909; shrink-on-load from WebP bytes gives 320x454
    /// </summary>
    [Theory]
    [InlineData(800, 1820, 200, 455)]
    [InlineData(640, 909, 320, 455)]
    [InlineData(200, 300, 200, 300)]
    [InlineData(2703, 1902, 320, 225)]
    public void FitWithin_Apply_ShrinksKeepingShape(int sourceWidth, int sourceHeight, int expectedWidth, int expectedHeight)
    {
        using var source = Image.Black(sourceWidth, sourceHeight, bands: 3);

        using var result = new FitWithinStep(320, 455).Apply(source);

        Assert.Equal((expectedWidth, expectedHeight), (result.Width, result.Height));
    }

    public static TheoryData<ImageKind, CoverImageSize, IImageStep[]> NewImageCases() => new()
    {
        { ImageKind.ChapterCover, CoverImageSize.Default, [new CoverThumbnailStep(320, 455)] },
        { ImageKind.SeriesCover, CoverImageSize.Medium, [new CoverThumbnailStep(640, 909)] },
        { ImageKind.LibraryIcon, CoverImageSize.Default, [new FitWithinStep(64, 64)] },
        { ImageKind.Favicon, CoverImageSize.Large, [new FitWithinStep(32, 32)] },
        { ImageKind.Bookmark, CoverImageSize.Default, [] },
    };

    [Theory]
    [MemberData(nameof(NewImageCases))]
    public void NewImageSteps_PerKind(ImageKind kind, CoverImageSize coverSize, IImageStep[] expected)
    {
        var steps = ImagePipeline.GetNewImageSteps(ImageProfile.For(kind), new ImagePolicy(EncodeFormat.WEBP, coverSize));

        Assert.Equal(expected, steps);
    }

    public static TheoryData<ImageKind, CoverImageSize, IImageStep[]> ReencodeCases() => new()
    {
        { ImageKind.ChapterCover, CoverImageSize.Default, [new FitWithinStep(320, 455)] },
        { ImageKind.SeriesCover, CoverImageSize.Medium, [new FitWithinStep(640, 909)] },
        { ImageKind.Publisher, CoverImageSize.Default, [new FitWithinStep(128, 128)] },
        { ImageKind.Bookmark, CoverImageSize.Default, [] },
    };

    /// <summary>
    /// A re-encode runs on locked and uploaded covers too, so it may only shrink: never a CoverThumbnailStep
    /// </summary>
    [Theory]
    [MemberData(nameof(ReencodeCases))]
    public void ReencodeSteps_PerKind_OnlyShrink(ImageKind kind, CoverImageSize coverSize, IImageStep[] expected)
    {
        var steps = ImagePipeline.GetReencodeSteps(ImageProfile.For(kind), new ImagePolicy(EncodeFormat.WEBP, coverSize));

        Assert.Equal(expected, steps);
    }

    /// <summary>
    /// Only a resize at index 0 is done while decoding; later it costs a full decode
    /// </summary>
    [Fact]
    public void Builders_PutResizeFirst()
    {
        var kobo = new[] { false, true }.Select(grey => KoboSyncService.GetKoboRender("355", "530", null, grey).Steps);
        var builders = Enum.GetValues<ImageKind>()
            .SelectMany(kind => Enum.GetValues<CoverImageSize>().SelectMany(size =>
            {
                var profile = ImageProfile.For(kind);
                var policy = new ImagePolicy(EncodeFormat.WEBP, size);
                return new[] { ImagePipeline.GetNewImageSteps(profile, policy), ImagePipeline.GetReencodeSteps(profile, policy) };
            }))
            .Concat(kobo);

        foreach (var steps in builders)
        {
            var resizeAt = steps.Select((s, i) => (s, i)).Where(x => x.s is IResizeStep).Select(x => x.i).ToList();
            Assert.True(resizeAt.Count == 0 || resizeAt.SequenceEqual([0]), $"[{string.Join(", ", steps)}]: resize at {string.Join(",", resizeAt)}");
        }
    }

    /// <summary>
    /// A width or height that does not parse leaves that side unbounded rather than failing the request
    /// </summary>
    [Theory]
    [InlineData("355", "530", "70", 355, 530, 70)]
    [InlineData("abc", "530", null, 1_000_000, 530, 85)]
    [InlineData("355", "530", "500", 355, 530, 100)]
    [InlineData("355", "530", "0", 355, 530, 1)]
    public void KoboRender_ParsesRequest(string width, string height, string? quality, int expectedWidth, int expectedHeight,
        int expectedQuality)
    {
        var render = KoboSyncService.GetKoboRender(width, height, quality, false);

        Assert.Equal(EncodeFormat.JPEG, render.Format);
        Assert.Equal(expectedQuality, render.Quality);
        Assert.Equal([new FitWithinStep(expectedWidth, expectedHeight)], render.Steps);
    }

    [Theory]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    public void Greyscale_Apply_OneColourBandKeepsAlpha(int bands, int expectedBands)
    {
        using var black = Image.Black(64, 64, bands: bands);
        using var source = black.Copy(interpretation: Enums.Interpretation.Srgb);

        using var result = new GreyscaleStep().Apply(source);

        Assert.Equal(expectedBands, result.Bands);
        Assert.Equal(Enums.Interpretation.Bw, result.Interpretation);
    }

    private static IEnumerable<string> AllSamples() => GetSamples(CoversDirectory).Concat(GetSamples(TransparencyDirectory));

    private static string CheckResult(StepCase stepCase, string sample, string outputPath)
    {
        using var source = Image.NewFromFile(sample);
        using var rendered = Image.NewFromBuffer(File.ReadAllBytes(outputPath));
        try
        {
            stepCase.Check(source, rendered);
            return "pass";
        }
        catch (Exception ex)
        {
            return "FAIL: " + ex.Message.ReplaceLineEndings(" ");
        }
    }

    private static void AssertShrunkToFit(Image source, Image rendered, int boxWidth, int boxHeight)
    {
        var scale = Math.Min(1.0, Math.Min((double) boxWidth / source.Width, (double) boxHeight / source.Height));
        Assert.InRange(rendered.Width, source.Width * scale - 1, source.Width * scale + 1);
        Assert.InRange(rendered.Height, source.Height * scale - 1, source.Height * scale + 1);
        Assert.True(rendered.Width <= boxWidth && rendered.Height <= boxHeight, $"{rendered.Width}x{rendered.Height} outside {boxWidth}x{boxHeight}");
    }

    private static void AssertGrey(Image image)
    {
        if (image.Bands < 3) return;

        using var red = image[0];
        using var green = image[1];
        using var blue = image[2];
        using var redGreen = (red - green).Abs();
        using var redBlue = (red - blue).Abs();
        Assert.True(Math.Max(redGreen.Max(), redBlue.Max()) <= 2, "colour bands differ");
    }

    /// <summary>
    /// The Kobo gets a JPEG within the requested box, and transparency flattened onto white, not black
    /// </summary>
    private static void AssertKoboJpeg(Image source, Image rendered)
    {
        Assert.Equal("jpegload_buffer", rendered.Get("vips-loader"));
        AssertShrunkToFit(source, rendered, 355, 530);
        if (!source.HasAlpha()) return;

        // Transparency samples are fully transparent in their top-left 200x200
        Assert.All(rendered.Getpoint(2, 2), channel => Assert.True(channel > 250, $"corner {channel}, not white"));
    }

    private sealed record StepCase(string Name, string Title, ImageRender Render, int? BoxWidth, int? BoxHeight,
        Action<Image, Image> Check);
}
