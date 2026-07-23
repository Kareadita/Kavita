using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NetVips;

namespace Kavita.Common.Extensions;

public static class ImageExtensions
{
    /// <summary>
    /// Structure to hold various image quality metrics
    /// </summary>
    private sealed class ImageQualityMetrics
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsColor { get; set; }
        public double Colorfulness { get; set; }
        public double Contrast { get; set; }
        public double Sharpness { get; set; }
        public double NoiseLevel { get; set; }
    }

    /// <summary>
    /// Calculate a similarity score (0-1f) based on resolution difference and MSE.
    /// </summary>
    /// <param name="imagePath1">Path to first image</param>
    /// <param name="imagePath2">Path to the second image</param>
    /// <returns>Similarity score between 0-1, where 1 is identical</returns>
    public static float CalculateSimilarity(this string imagePath1, string imagePath2)
    {
        if (!File.Exists(imagePath1) || !File.Exists(imagePath2))
        {
            throw new FileNotFoundException("One or both image files do not exist");
        }

        using var img1 = LoadRgb(imagePath1);
        using var img2 = LoadRgb(imagePath2);

        // Calculate resolution difference factor
        var res1 = img1.Width * img1.Height;
        var res2 = img2.Width * img2.Height;
        var resolutionDiff = Math.Abs(res1 - res2) / (float) Math.Max(res1, res2);

        // Calculate mean squared error for pixel differences
        var mse = img1.GetMeanSquaredError(img2);

        // Normalize MSE (65025 = 255², which is the max possible squared difference per channel)
        var normalizedMse = 1f - Math.Min(1f, mse / 65025f);

        // Final similarity score (weighted average of resolution difference and color difference)
        return Math.Max(0f, 1f - (resolutionDiff * 0.5f) - (1f - normalizedMse) * 0.5f);
    }

    /// <summary>
    /// Determines which image is "better" based on multiple quality factors.
    /// </summary>
    /// <param name="imagePath1">Path to first image</param>
    /// <param name="imagePath2">Path to the second image</param>
    /// <param name="preferColor">Whether to prefer color images over grayscale (default: true)</param>
    /// <returns>The path of the better image</returns>
    public static string GetBetterImage(this string imagePath1, string imagePath2, bool preferColor = true)
    {
        if (!File.Exists(imagePath1) || !File.Exists(imagePath2))
        {
            throw new FileNotFoundException("One or both image files do not exist");
        }

        // Quick metadata check to get width/height without decoding full pixel data
        var (width1, height1) = GetDimensions(imagePath1);
        var (width2, height2) = GetDimensions(imagePath2);

        // Calculate resolution factor
        double resolutionFactor1 = width1 * height1;
        double resolutionFactor2 = width2 * height2;

        // If one image is significantly higher resolution (3x or more), just pick it
        // This avoids fully loading both images when the choice is obvious
        if (resolutionFactor1 > resolutionFactor2 * 3)
            return imagePath1;
        if (resolutionFactor2 > resolutionFactor1 * 3)
            return imagePath2;

        // Otherwise, we need to analyze the actual image data for both
        ImageQualityMetrics metrics1;

        // NOTE: We HAVE to use these scope blocks and load image here otherwise memory-mapped section exception will occur
        using (var img1 = Image.NewFromFile(imagePath1))
        {
            metrics1 = GetImageQualityMetrics(img1);
        }

        ImageQualityMetrics metrics2;
        using (var img2 = Image.NewFromFile(imagePath2))
        {
            metrics2 = GetImageQualityMetrics(img2);
        }

        // If one is color, and one is grayscale, then we prefer color
        if (preferColor && metrics1.IsColor != metrics2.IsColor)
        {
            return metrics1.IsColor ? imagePath1 : imagePath2;
        }

        // Calculate overall quality scores
        var score1 = CalculateOverallScore(metrics1);
        var score2 = CalculateOverallScore(metrics2);

        return score1 >= score2 ? imagePath1 : imagePath2;
    }

    /// <summary>
    /// Smaller is better
    /// </summary>
    /// <param name="img1"></param>
    /// <param name="img2"></param>
    /// <returns></returns>
    public static float GetMeanSquaredError(this Image img1, Image img2)
    {
        // If the dimensions differ, resample img2 up/down to img1's size, then compare
        // pixel-by-pixel over the RGB bands (alpha is ignored).
        if (img1.Width != img2.Width || img1.Height != img2.Height)
        {
            var hscale = (double) img1.Width / img2.Width;
            var vscale = (double) img1.Height / img2.Height;
            img2 = img2.Resize(hscale, vscale: vscale);
            img2 = ForceSize(img2, img1.Width, img1.Height);
        }

        // Work in float so subtraction cannot wrap around (uchar - uchar would clip/overflow).
        var f1 = img1.Cast(Enums.BandFormat.Float);
        var f2 = img2.Cast(Enums.BandFormat.Float);

        var diff = f1 - f2;
        var squared = diff * diff;

        // Avg() is the mean over every band-element. We sum R²+G²+B² per pixel and divide by the
        // pixel count only, so multiply the per-element mean back up by the band count.
        var meanSquaredPerElement = squared.Avg();
        return (float) (meanSquaredPerElement * img1.Bands);
    }

    /// <summary>
    /// Calculate a weighted overall score based on metrics
    /// </summary>
    private static double CalculateOverallScore(ImageQualityMetrics metrics)
    {
        // Resolution factor (normalized to HD resolution)
        var resolutionFactor = Math.Min(1.0, (metrics.Width * metrics.Height) / (double) (1920 * 1080));

        // Color factor
        var colorFactor = metrics.IsColor ? (0.5 + 0.5 * metrics.Colorfulness) : 0.3;

        // Quality factors
        var contrastFactor = Math.Min(1.0, metrics.Contrast);
        var sharpnessFactor = Math.Min(1.0, metrics.Sharpness);

        // Noise penalty (less noise is better)
        var noisePenalty = Math.Max(0, 1.0 - metrics.NoiseLevel);

        // Weighted combination
        return (resolutionFactor * 0.35) +
               (colorFactor * 0.3) +
               (contrastFactor * 0.15) +
               (sharpnessFactor * 0.15) +
               (noisePenalty * 0.05);
    }

    /// <summary>
    /// Gets quality metrics for an image
    /// </summary>
    private static ImageQualityMetrics GetImageQualityMetrics(Image image)
    {
        // Create a smaller version if the image is large to speed up analysis.
        // This bounds the longest side to 512 while preserving aspect ratio.
        var workingImage = image;
        if (image.Width > 512 || image.Height > 512)
        {
            var scale = 512.0 / Math.Max(image.Width, image.Height);
            workingImage = image.Resize(scale);
        }

        var buffer = ToRgbBuffer(workingImage);

        var metrics = new ImageQualityMetrics
        {
            Width = image.Width,
            Height = image.Height
        };

        // Color analysis (is the image color or grayscale?)
        var colorInfo = AnalyzeColorfulness(buffer);
        metrics.IsColor = colorInfo.IsColor;
        metrics.Colorfulness = colorInfo.Colorfulness;

        // Contrast analysis
        metrics.Contrast = CalculateContrast(buffer);

        // Sharpness estimation
        metrics.Sharpness = EstimateSharpness(buffer);

        // Noise estimation
        metrics.NoiseLevel = EstimateNoiseLevel(buffer);

        return metrics;
    }

    /// <summary>
    /// Analyzes colorfulness of an image
    /// </summary>
    private static (bool IsColor, double Colorfulness) AnalyzeColorfulness(RgbBuffer image)
    {
        // For performance, sample a subset of pixels
        var sampleSize = Math.Min(1000, image.Width * image.Height);
        var stepSize = Math.Max(1, (image.Width * image.Height) / sampleSize);

        var colorCount = 0;
        List<(int R, int G, int B)> samples = [];

        // Sample pixels
        for (var i = 0; i < image.Width * image.Height; i += stepSize)
        {
            var x = i % image.Width;
            var y = i / image.Width;

            var pixel = image[x, y];

            // Check if RGB channels differ by a threshold
            // High difference indicates color, low difference indicates grayscale
            var rMinusG = Math.Abs(pixel.R - pixel.G);
            var rMinusB = Math.Abs(pixel.R - pixel.B);
            var gMinusB = Math.Abs(pixel.G - pixel.B);

            if (rMinusG > 15 || rMinusB > 15 || gMinusB > 15)
            {
                colorCount++;
            }

            samples.Add((pixel.R, pixel.G, pixel.B));
        }

        // Calculate colorfulness metric based on Hasler and Süsstrunk's approach
        // This measures the spread and intensity of colors
        if (samples.Count <= 0) return (false, 0);

        // Calculate rg and yb opponent channels
        var rg = samples.Select(p => p.R - p.G).ToList();
        var yb = samples.Select(p => 0.5 * (p.R + p.G) - p.B).ToList();

        // Calculate standard deviation and mean of opponent channels
        var rgStdDev = CalculateStdDev(rg);
        var ybStdDev = CalculateStdDev(yb);
        var rgMean = rg.Average();
        var ybMean = yb.Average();

        // Combine into colorfulness metric
        var stdRoot = Math.Sqrt(rgStdDev * rgStdDev + ybStdDev * ybStdDev);
        var meanRoot = Math.Sqrt(rgMean * rgMean + ybMean * ybMean);

        var colorfulness = stdRoot + 0.3 * meanRoot;

        // Normalize to 0-1 range (typical colorfulness is 0-100)
        colorfulness = Math.Min(1.0, colorfulness / 100.0);

        var isColor = (double)colorCount / samples.Count > 0.05;

        return (isColor, colorfulness);
    }

    /// <summary>
    /// Calculate standard deviation of a list of values
    /// </summary>
    private static double CalculateStdDev(List<int> values)
    {
        var mean = values.Average();
        var sumOfSquaresOfDifferences = values.Select(val => (val - mean) * (val - mean)).Sum();
        return Math.Sqrt(sumOfSquaresOfDifferences / values.Count);
    }

    /// <summary>
    /// Calculate standard deviation of a list of values
    /// </summary>
    private static double CalculateStdDev(List<double> values)
    {
        var mean = values.Average();
        var sumOfSquaresOfDifferences = values.Select(val => (val - mean) * (val - mean)).Sum();
        return Math.Sqrt(sumOfSquaresOfDifferences / values.Count);
    }

    /// <summary>
    /// Calculates contrast of an image
    /// </summary>
    private static double CalculateContrast(RgbBuffer image)
    {
        // For performance, sample a subset of pixels
        var sampleSize = Math.Min(1000, image.Width * image.Height);
        var stepSize = Math.Max(1, (image.Width * image.Height) / sampleSize);

        List<int> luminanceValues = new();

        // Sample pixels and calculate luminance
        for (var i = 0; i < image.Width * image.Height; i += stepSize)
        {
            var x = i % image.Width;
            var y = i / image.Width;

            var pixel = image[x, y];

            // Calculate luminance
            var luminance = (int)(0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B);
            luminanceValues.Add(luminance);
        }

        if (luminanceValues.Count < 2)
            return 0;

        // Use RMS contrast (root-mean-square of pixel intensity)
        var mean = luminanceValues.Average();
        var sumOfSquaresOfDifferences = luminanceValues.Sum(l => Math.Pow(l - mean, 2));
        var rmsContrast = Math.Sqrt(sumOfSquaresOfDifferences / luminanceValues.Count) / mean;

        // Normalize to 0-1 range
        return Math.Min(1.0, rmsContrast);
    }

    /// <summary>
    /// Estimates sharpness using simple Laplacian-based method
    /// </summary>
    private static double EstimateSharpness(RgbBuffer image)
    {
        // For simplicity, convert to grayscale
        var grayImage = new int[image.Width, image.Height];

        // Convert to grayscale
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var pixel = image[x, y];
                grayImage[x, y] = (int)(0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B);
            }
        }

        // Apply Laplacian filter (3x3)
        // The Laplacian measures local variations - higher values indicate edges/details
        double laplacianSum = 0;
        var validPixels = 0;

        // Laplacian kernel: [0, 1, 0, 1, -4, 1, 0, 1, 0]
        for (var y = 1; y < image.Height - 1; y++)
        {
            for (var x = 1; x < image.Width - 1; x++)
            {
                var laplacian =
                    grayImage[x, y - 1] +
                    grayImage[x - 1, y] - 4 * grayImage[x, y] + grayImage[x + 1, y] +
                    grayImage[x, y + 1];

                laplacianSum += Math.Abs(laplacian);
                validPixels++;
            }
        }

        if (validPixels == 0)
            return 0;

        // Calculate variance of Laplacian
        var laplacianVariance = laplacianSum / validPixels;

        // Normalize to 0-1 range (typical values range from 0-1000)
        return Math.Min(1.0, laplacianVariance / 1000.0);
    }

    /// <summary>
    /// Estimates noise level using simple block-based variance method
    /// </summary>
    private static double EstimateNoiseLevel(RgbBuffer image)
    {
        // Block size for noise estimation
        const int blockSize = 8;
        List<double> blockVariances = new();

        // Calculate variance in small blocks throughout the image
        for (var y = 0; y < image.Height - blockSize; y += blockSize)
        {
            for (var x = 0; x < image.Width - blockSize; x += blockSize)
            {
                List<int> blockValues = new();

                // Sample block
                for (var by = 0; by < blockSize; by++)
                {
                    for (var bx = 0; bx < blockSize; bx++)
                    {
                        var pixel = image[x + bx, y + by];
                        var value = (int)(0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B);
                        blockValues.Add(value);
                    }
                }

                // Calculate variance of this block
                var blockMean = blockValues.Average();
                var blockVariance = blockValues.Sum(v => Math.Pow(v - blockMean, 2)) / blockValues.Count;
                blockVariances.Add(blockVariance);
            }
        }

        if (blockVariances.Count == 0)
            return 0;

        // Sort block variances and take lowest 10% (likely uniform areas where noise is most visible)
        blockVariances.Sort();
        var smoothBlocksCount = Math.Max(1, blockVariances.Count / 10);
        var averageNoiseVariance = blockVariances.Take(smoothBlocksCount).Average();

        // Normalize to 0-1 range (typical noise variances are 0-100)
        return Math.Min(1.0, averageNoiseVariance / 100.0);
    }

    /// <summary>
    /// Reads width/height from an image header without decoding the pixel data. NetVips loads
    /// lazily, so reading the properties is cheap.
    /// </summary>
    private static (int Width, int Height) GetDimensions(string path)
    {
        using var img = Image.NewFromFile(path, access: Enums.Access.Sequential);
        return (img.Width, img.Height);
    }

    /// <summary>
    /// Loads an image and normalizes it to 3-band 8-bit sRGB so downstream pixel math can assume a
    /// fixed R,G,B layout, regardless of the source format (1-band grayscale, 4-band CMYK, etc.).
    /// </summary>
    private static Image LoadRgb(string path)
    {
        using var img = Image.NewFromFile(path);
        return Normalize(img);
    }

    /// <summary>
    /// Normalizes any image to 3-band 8-bit sRGB (drops alpha, promotes grayscale/CMYK, casts to uchar).
    /// </summary>
    private static Image Normalize(Image img)
    {
        var result = img;

        if (result.Interpretation != Enums.Interpretation.Srgb)
        {
            result = result.Colourspace(Enums.Interpretation.Srgb);
        }

        if (result.HasAlpha())
        {
            result = result.Flatten();
        }

        if (result.Format != Enums.BandFormat.Uchar)
        {
            result = result.Cast(Enums.BandFormat.Uchar);
        }

        // Ensure we always return a distinct instance, otherwise the caller's original instance will get disposed incorrectly
        return ReferenceEquals(result, img) ? img.Copy() : result;
    }

    /// <summary>
    /// Materializes a NetVips image into a fixed-layout RGB byte buffer for per-pixel math. This is
    /// the one native round-trip; all pixel loops index the managed buffer.
    /// </summary>
    private static RgbBuffer ToRgbBuffer(Image image)
    {
        using var normalized = Normalize(image);
        var bytes = normalized.WriteToMemory<byte>();
        return new RgbBuffer(normalized.Width, normalized.Height, bytes);
    }

    /// <summary>
    /// Forces an image to exact dimensions after a resize, guarding against NetVips' rounding
    /// producing an off-by-one width/height (which would make band arithmetic throw).
    /// </summary>
    private static Image ForceSize(Image img, int width, int height)
    {
        if (img.Width == width && img.Height == height)
        {
            return img;
        }

        if (img.Width > width || img.Height > height)
        {
            img = img.Crop(0, 0, Math.Min(img.Width, width), Math.Min(img.Height, height));
        }

        if (img.Width != width || img.Height != height)
        {
            img = img.Embed(0, 0, width, height, extend: Enums.Extend.Copy);
        }

        return img;
    }

    /// <summary>
    /// A fixed-layout, row-major, 3-band (R,G,B) 8-bit view over a materialized NetVips image. Pixel
    /// access is managed array indexing, so the pixel-heavy analysis loops avoid native calls.
    /// </summary>
    private readonly struct RgbBuffer(int width, int height, byte[] data)
    {
        public int Width { get; } = width;
        public int Height { get; } = height;

        public Pixel this[int x, int y]
        {
            get
            {
                var offset = (y * Width + x) * 3;
                return new Pixel(data[offset], data[offset + 1], data[offset + 2]);
            }
        }
    }

    /// <summary>A single RGB sample.</summary>
    private readonly struct Pixel(byte r, byte g, byte b)
    {
        public byte R { get; } = r;
        public byte G { get; } = g;
        public byte B { get; } = b;
    }
}
