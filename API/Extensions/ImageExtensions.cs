using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;

namespace API.Extensions;

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

        // Load both images as Rgba32 (consistent with the rest of the code)
        using var img1 = Image.Load<Rgba32>(imagePath1);
        using var img2 = Image.Load<Rgba32>(imagePath2);

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
    /// Smaller is better
    /// </summary>
    /// <param name="img1"></param>
    /// <param name="img2"></param>
    /// <returns></returns>
    public static float GetMeanSquaredError(this Image<Rgba32> img1, Image<Rgba32> img2)
    {
        if (img1.Width != img2.Width || img1.Height != img2.Height)
        {
            img2.Mutate(x => x.Resize(img1.Width, img1.Height));
        }

        double totalDiff = 0;
        for (var y = 0; y < img1.Height; y++)
        {
            for (var x = 0; x < img1.Width; x++)
            {
                var pixel1 = img1[x, y];
                var pixel2 = img2[x, y];

                var diff = Math.Pow(pixel1.R - pixel2.R, 2) +
                           Math.Pow(pixel1.G - pixel2.G, 2) +
                           Math.Pow(pixel1.B - pixel2.B, 2);
                totalDiff += diff;
            }
        }

        return (float) (totalDiff / (img1.Width * img1.Height));
    }

    /// <summary>
    /// Determines which image is "better" based on multiple quality factors
    /// using only the cross-platform ImageSharp library
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

        // Quick metadata check to get width/height without loading full pixel data
        var info1 = Image.Identify(imagePath1);
        var info2 = Image.Identify(imagePath2);

        // Calculate resolution factor
        double resolutionFactor1 = info1.Width * info1.Height;
        double resolutionFactor2 = info2.Width * info2.Height;

        // If one image is significantly higher resolution (3x or more), just pick it
        // This avoids fully loading both images when the choice is obvious
        if (resolutionFactor1 > resolutionFactor2 * 3)
            return imagePath1;
        if (resolutionFactor2 > resolutionFactor1 * 3)
            return imagePath2;

        // Otherwise, we need to analyze the actual image data for both

        // NOTE: We HAVE to use these scope blocks and load image here otherwise memory-mapped section exception will occur
        ImageQualityMetrics metrics1;
        using (var img1 = Image.Load<Rgba32>(imagePath1))
        {
            metrics1 = GetImageQualityMetrics(img1);
        }

        ImageQualityMetrics metrics2;
        using (var img2 = Image.Load<Rgba32>(imagePath2))
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
    private static ImageQualityMetrics GetImageQualityMetrics(Image<Rgba32> image)
    {
        // Create a smaller version if the image is large to speed up analysis
        Image<Rgba32> workingImage;
        if (image.Width > 512 || image.Height > 512)
        {
            workingImage = image.Clone(ctx => ctx.Resize(
                new ResizeOptions {
                    Size = new Size(512),
                    Mode = ResizeMode.Max
                }));
        }
        else
        {
            workingImage = image.Clone();
        }

        var metrics = new ImageQualityMetrics
        {
            Width = image.Width,
            Height = image.Height
        };

        // Color analysis (is the image color or grayscale?)
        var colorInfo = AnalyzeColorfulness(workingImage);
        metrics.IsColor = colorInfo.IsColor;
        metrics.Colorfulness = colorInfo.Colorfulness;

        // Contrast analysis
        metrics.Contrast = CalculateContrast(workingImage);

        // Sharpness estimation
        metrics.Sharpness = EstimateSharpness(workingImage);

        // Noise estimation
        metrics.NoiseLevel = EstimateNoiseLevel(workingImage);

        // Clean up
        workingImage.Dispose();

        return metrics;
    }

    /// <summary>
    /// Analyzes colorfulness of an image
    /// </summary>
    private static (bool IsColor, double Colorfulness) AnalyzeColorfulness(Image<Rgba32> image)
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
    private static double CalculateContrast(Image<Rgba32> image)
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
    private static double EstimateSharpness(Image<Rgba32> image)
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
    private static double EstimateNoiseLevel(Image<Rgba32> image)
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
}
