using System;
using System.IO;
using NetVips;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Image = NetVips.Image;

namespace API.Extensions;

public static class ImageExtensions
{
    public static int GetResolution(this Image image)
    {
        return image.Width * image.Height;
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

        return (float)(totalDiff / (img1.Width * img1.Height));
    }

    public static float GetSimilarity(this string imagePath1, string imagePath2)
    {
        if (!File.Exists(imagePath1) || !File.Exists(imagePath2))
        {
            throw new FileNotFoundException("One or both image files do not exist");
        }

        // Calculate similarity score
        return CalculateSimilarity(imagePath1, imagePath2);
    }

    /// <summary>
    /// Determines which image is "better" based on similarity and resolution.
    /// </summary>
    /// <param name="imagePath1">Path to first image</param>
    /// <param name="imagePath2">Path to second image</param>
    /// <param name="similarityThreshold">Minimum similarity to consider images similar</param>
    /// <returns>The path of the better image</returns>
    public static string GetBetterImage(this string imagePath1, string imagePath2, float similarityThreshold = 0.7f)
    {
        if (!File.Exists(imagePath1) || !File.Exists(imagePath2))
        {
            throw new FileNotFoundException("One or both image files do not exist");
        }

        // Calculate similarity score
        var similarity = CalculateSimilarity(imagePath1, imagePath2);

        using var img1 = Image.NewFromFile(imagePath1, access: Enums.Access.Sequential);
        using var img2 = Image.NewFromFile(imagePath2, access: Enums.Access.Sequential);

        var resolution1 = img1.Width * img1.Height;
        var resolution2 = img2.Width * img2.Height;

        // If images are similar, choose the one with higher resolution
        if (similarity >= similarityThreshold)
        {
            return resolution1 >= resolution2 ? imagePath1 : imagePath2;
        }

        // If images are not similar, allow the new image
        return imagePath2;
    }

    /// <summary>
    /// Calculate a similarity score (0-1f) based on resolution difference and MSE.
    /// </summary>
    /// <param name="imagePath1"></param>
    /// <param name="imagePath2"></param>
    /// <returns></returns>
    private static float CalculateSimilarity(string imagePath1, string imagePath2)
    {
        if (!File.Exists(imagePath1) || !File.Exists(imagePath2))
        {
            return -1;
        }

        using var img1 = Image.NewFromFile(imagePath1, access: Enums.Access.Sequential);
        using var img2 = Image.NewFromFile(imagePath2, access: Enums.Access.Sequential);

        var res1 = img1.Width * img1.Height;
        var res2 = img2.Width * img2.Height;
        var resolutionDiff = Math.Abs(res1 - res2) / (float)Math.Max(res1, res2);

        using var imgSharp1 = SixLabors.ImageSharp.Image.Load<Rgba32>(imagePath1);
        using var imgSharp2 = SixLabors.ImageSharp.Image.Load<Rgba32>(imagePath2);

        var mse = imgSharp1.GetMeanSquaredError(imgSharp2);
        var normalizedMse = 1f - Math.Min(1f, mse / 65025f); // Normalize based on max color diff

        // Final similarity score (weighted)
        return Math.Max(0f, 1f - (resolutionDiff * 0.5f) - (1f - normalizedMse) * 0.5f);
    }
}
