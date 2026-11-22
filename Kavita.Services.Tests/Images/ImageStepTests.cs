using Kavita.Models.Entities.Enums;
using Kavita.Models.Images;
using Kavita.Services.Images;
using Kavita.Services.Images.ImageSteps;
using NetVips;

namespace Kavita.Services.Tests.Images;

public class ImageStepTests
{
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
    [InlineData(2703, 1902)]
    [InlineData(160, 227)]
    public void CoverThumbnail_Apply_ExactlyCoverSize(int sourceWidth, int sourceHeight)
    {
        using var source = Image.Black(sourceWidth, sourceHeight, bands: 3);

        using var result = new CoverThumbnailStep(320, 455).Apply(source);

        Assert.Equal((320, 455), (result.Width, result.Height));
    }

    [Theory]
    [InlineData(600, 1200)]
    [InlineData(100, 100)]
    public void FitWithin_TargetFor_IgnoresSourceAndNeverEnlarges(int sourceWidth, int sourceHeight)
    {
        var target = new FitWithinStep(320, 455).TargetFor(sourceWidth, sourceHeight);

        Assert.Equal((320, 455, Enums.Size.Down, (Enums.Interesting?) null), (target.Width, target.Height, target.Size, target.Crop));
    }

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
    /// Only a resize at index 0 is done while decoding; later it costs a full decode (Stream vs File in the baseline)
    /// </summary>
    [Fact]
    public void Builders_PutResizeFirst()
    {
        foreach (var kind in Enum.GetValues<ImageKind>())
        {
            var profile = ImageProfile.For(kind);
            foreach (var size in Enum.GetValues<CoverImageSize>())
            {
                var policy = new ImagePolicy(EncodeFormat.WEBP, size);
                foreach (var steps in new[] { ImagePipeline.GetNewImageSteps(profile, policy), ImagePipeline.GetReencodeSteps(profile, policy) })
                {
                    var resizeAt = steps.Select((s, i) => (s, i)).Where(x => x.s is IResizeStep).Select(x => x.i).ToList();
                    Assert.True(resizeAt.Count == 0 || resizeAt.SequenceEqual([0]), $"{kind} at {size}: resize at {string.Join(",", resizeAt)}");
                }
            }
        }
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
}
