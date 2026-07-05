using Kavita.Imaging.Tests.Backends.ImageSharp;

namespace Kavita.Imaging.Tests;

/// <summary>
/// Runs the shared behaviour suite against the frozen ImageSharp reference implementation.
/// These are the oracle: they must always pass.
/// </summary>
public class ImageSharpImageExtensionsTests : ImageExtensionsTestsBase
{
    public ImageSharpImageExtensionsTests(ImageFixture images) : base(images) { }

    protected override float CalculateSimilarity(string imagePath1, string imagePath2)
        => imagePath1.CalculateSimilarity(imagePath2);

    protected override string GetBetterImage(string imagePath1, string imagePath2, bool preferColor)
        => imagePath1.GetBetterImage(imagePath2, preferColor);
}
