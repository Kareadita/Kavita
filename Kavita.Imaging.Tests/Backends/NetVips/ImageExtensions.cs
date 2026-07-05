using System;

namespace Kavita.Imaging.Tests.Backends.NetVips;

/// <summary>
/// In-progress NetVips implementation of the ImageExtensions surface.
/// Migrate one method at a time. As each method is implemented, remove the matching
/// [Skip] from <see cref="Kavita.Imaging.Tests.NetVipsImageExtensionsTests"/> so its
/// tests run against the shared behaviour suite (validated by the frozen ImageSharp oracle).
///
/// When the migration is complete, this class can be lifted verbatim back into
/// Kavita.Common.Extensions.ImageExtensions.
/// </summary>
public static class ImageExtensions
{
    /// <summary>
    /// Calculate a similarity score (0-1f) based on resolution difference and MSE.
    /// </summary>
    /// <param name="imagePath1">Path to first image</param>
    /// <param name="imagePath2">Path to the second image</param>
    /// <returns>Similarity score between 0-1, where 1 is identical</returns>
    public static float CalculateSimilarity(this string imagePath1, string imagePath2)
    {
        throw new NotImplementedException("NetVips migration pending");
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
        throw new NotImplementedException("NetVips migration pending");
    }
}
