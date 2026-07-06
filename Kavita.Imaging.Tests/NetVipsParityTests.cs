using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ImageSharpExt = Kavita.Imaging.Tests.Backends.ImageSharp.ImageExtensions;
using NetVipsExt = Kavita.Imaging.Tests.Backends.NetVips.ImageExtensions;

namespace Kavita.Imaging.Tests;

/// <summary>
/// Numeric parity tests: the behaviour suite only proves the NetVips output is *sane*
/// (in range, prefers color, etc). These assert the NetVips numbers stay close to the frozen
/// ImageSharp oracle for the same inputs, within an explicit tolerance, so a lift back into
/// Kavita.Common cannot silently change real cover-selection decisions.
///
/// Tolerance note: same-size pairs must match exactly (no resampling). The only source of
/// divergence is the resize path (NetVips kernel != ImageSharp bicubic), exercised by the
/// HighRes/LowRes pair. <see cref="SimilarityTolerance"/> is sized to bound that.
/// </summary>
public class NetVipsParityTests : IClassFixture<ImageFixture>
{
    private const float SimilarityTolerance = 0.02f;

    private readonly ImageFixture _images;

    public NetVipsParityTests(ImageFixture images)
    {
        _images = images;
    }

    public static IEnumerable<object[]> SimilarityPairs()
    {
        yield return ["identical"];
        yield return ["color-vs-gray"];
        yield return ["highres-vs-lowres"];
    }

    [Theory]
    [MemberData(nameof(SimilarityPairs))]
    public void CalculateSimilarity_MatchesOracle(string pair)
    {
        var (a, b) = Resolve(pair);

        var expected = ImageSharpExt.CalculateSimilarity(a, b);
        var actual = NetVipsExt.CalculateSimilarity(a, b);

        Assert.InRange(actual, expected - SimilarityTolerance, expected + SimilarityTolerance);
    }

    [Fact]
    public void CalculateSimilarity_IdenticalImages_ExactlyMatchesOracle()
    {
        // Byte-identical, same-size inputs => MSE is exactly 0 in both backends, so the whole
        // score must match to the float, not just within tolerance.
        var expected = ImageSharpExt.CalculateSimilarity(_images.ColorfulA, _images.ColorfulACopy);
        var actual = NetVipsExt.CalculateSimilarity(_images.ColorfulA, _images.ColorfulACopy);

        Assert.Equal(expected, actual);
    }

    private (string, string) Resolve(string pair) => pair switch
    {
        "identical" => (_images.ColorfulA, _images.ColorfulACopy),
        "color-vs-gray" => (_images.ColorfulA, _images.Grayscale),
        "highres-vs-lowres" => (_images.HighRes, _images.LowRes),
        _ => throw new System.ArgumentOutOfRangeException(nameof(pair), pair, null)
    };

    // ---- Per-metric parity ---------------------------------------------------------------------
    //
    // These feed the SAME pixels to both backends so any delta is purely algorithmic, not
    // resampling. They use only images whose longest side is <= 512 (ColorfulA/Grayscale/LowRes),
    // because for those GetImageQualityMetrics does no resize -- so calling the metric helpers on the
    // raw file matches exactly what the real code path would see. The image is byte-for-byte decoded
    // by both libraries, so parity here should be near-exact (MetricTolerance guards float rounding).

    private const double MetricTolerance = 1e-6;

    public static IEnumerable<object[]> NoResizeImages()
    {
        yield return ["colorful"];
        yield return ["grayscale"];
        yield return ["lowres"];
        // True 1-band grayscale: exercises NetVips' Colourspace(b-w -> sRGB) promotion in Normalize,
        // the path real grayscale covers hit and the 4-band fixtures never reach. Matches exactly.
        yield return ["grayscale-1band"];
    }

    [Theory]
    [MemberData(nameof(NoResizeImages))]
    public void AnalyzeColorfulness_MatchesOracle(string image)
    {
        var path = ResolveSingle(image);

        using var sharp = Image.Load<Rgba32>(path);
        var (expectedIsColor, expectedColorfulness) = ImageSharpExt.AnalyzeColorfulness(sharp);

        var buffer = NetVipsExt.LoadRgbBuffer(path);
        var (actualIsColor, actualColorfulness) = NetVipsExt.AnalyzeColorfulness(buffer);

        Assert.Equal(expectedIsColor, actualIsColor);
        Assert.InRange(actualColorfulness, expectedColorfulness - MetricTolerance, expectedColorfulness + MetricTolerance);
    }

    [Theory]
    [MemberData(nameof(NoResizeImages))]
    public void CalculateContrast_MatchesOracle(string image)
    {
        var path = ResolveSingle(image);

        using var sharp = Image.Load<Rgba32>(path);
        var expected = ImageSharpExt.CalculateContrast(sharp);

        var actual = NetVipsExt.CalculateContrast(NetVipsExt.LoadRgbBuffer(path));

        Assert.InRange(actual, expected - MetricTolerance, expected + MetricTolerance);
    }

    [Theory]
    [MemberData(nameof(NoResizeImages))]
    public void EstimateSharpness_MatchesOracle(string image)
    {
        var path = ResolveSingle(image);

        using var sharp = Image.Load<Rgba32>(path);
        var expected = ImageSharpExt.EstimateSharpness(sharp);

        var actual = NetVipsExt.EstimateSharpness(NetVipsExt.LoadRgbBuffer(path));

        Assert.InRange(actual, expected - MetricTolerance, expected + MetricTolerance);
    }

    [Theory]
    [MemberData(nameof(NoResizeImages))]
    public void EstimateNoiseLevel_MatchesOracle(string image)
    {
        var path = ResolveSingle(image);

        using var sharp = Image.Load<Rgba32>(path);
        var expected = ImageSharpExt.EstimateNoiseLevel(sharp);

        var actual = NetVipsExt.EstimateNoiseLevel(NetVipsExt.LoadRgbBuffer(path));

        Assert.InRange(actual, expected - MetricTolerance, expected + MetricTolerance);
    }

    // ---- 16-bit downconversion parity ----------------------------------------------------------
    //
    // A true 16-bit grayscale PNG must go through Normalize's 16->8-bit downconversion. NetVips
    // (Colourspace) and ImageSharp (Load<Rgba32>) both scale correctly, but round the last bit
    // slightly differently, so the metrics diverge by a small, bounded amount (measured < 0.4%).
    // This is a KNOWN, documented deviation: 16-bit sources are effectively never seen for covers,
    // and this margin cannot flip a GetBetterImage decision outside a razor-edge tie. If this ever
    // fails by a wide margin, Normalize is mis-scaling (e.g. clipping instead of scaling) -- a bug.

    private const double DownconvertTolerance = 0.01;

    [Fact]
    public void CalculateContrast_SixteenBit_WithinBoundedTolerance()
    {
        using var sharp = Image.Load<Rgba32>(_images.Grayscale16Bit);
        var expected = ImageSharpExt.CalculateContrast(sharp);
        var actual = NetVipsExt.CalculateContrast(NetVipsExt.LoadRgbBuffer(_images.Grayscale16Bit));
        Assert.InRange(actual, expected - DownconvertTolerance, expected + DownconvertTolerance);
    }

    [Fact]
    public void EstimateNoiseLevel_SixteenBit_WithinBoundedTolerance()
    {
        using var sharp = Image.Load<Rgba32>(_images.Grayscale16Bit);
        var expected = ImageSharpExt.EstimateNoiseLevel(sharp);
        var actual = NetVipsExt.EstimateNoiseLevel(NetVipsExt.LoadRgbBuffer(_images.Grayscale16Bit));
        Assert.InRange(actual, expected - DownconvertTolerance, expected + DownconvertTolerance);
    }

    // ---- GetBetterImage parity -----------------------------------------------------------------
    //
    // End-to-end: assert the NetVips decision matches the oracle's chosen path. Exercises the resize
    // path (HighRes -> 512) plus CalculateOverallScore, so it bounds any resampling divergence at the
    // level that actually matters: which file wins.

    public static IEnumerable<object[]> BetterImagePairs()
    {
        yield return ["colorful", "lowres"];
        yield return ["colorful", "grayscale"];
        yield return ["highres", "colorful"];
        yield return ["grayscale", "lowres"];
    }

    [Theory]
    [MemberData(nameof(BetterImagePairs))]
    public void GetBetterImage_MatchesOracle(string first, string second)
    {
        var a = ResolveSingle(first);
        var b = ResolveSingle(second);

        var expected = ImageSharpExt.GetBetterImage(a, b, true);
        var actual = NetVipsExt.GetBetterImage(a, b, true);

        Assert.Equal(expected, actual);
    }

    private string ResolveSingle(string image) => image switch
    {
        "colorful" => _images.ColorfulA,
        "grayscale" => _images.Grayscale,
        "grayscale-1band" => _images.GrayscaleOneBand,
        "lowres" => _images.LowRes,
        "highres" => _images.HighRes,
        _ => throw new System.ArgumentOutOfRangeException(nameof(image), image, null)
    };
}
