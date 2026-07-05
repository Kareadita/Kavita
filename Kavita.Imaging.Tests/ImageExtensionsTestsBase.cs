using System.IO;

namespace Kavita.Imaging.Tests;

/// <summary>
/// Backend-agnostic behaviour suite for ImageExtensions. Each concrete backend
/// (ImageSharp reference, NetVips in-progress) derives from this and forwards the two
/// public entry points to its own static extension methods. The assertions here describe
/// behaviour that must hold regardless of imaging library, so a passing NetVips subclass
/// proves parity with the frozen ImageSharp oracle.
/// </summary>
public abstract class ImageExtensionsTestsBase : IClassFixture<ImageFixture>
{
    protected readonly ImageFixture Images;

    protected ImageExtensionsTestsBase(ImageFixture images)
    {
        Images = images;
    }

    /// <summary>Forwards to the backend's CalculateSimilarity extension method.</summary>
    protected abstract float CalculateSimilarity(string imagePath1, string imagePath2);

    /// <summary>Forwards to the backend's GetBetterImage extension method.</summary>
    protected abstract string GetBetterImage(string imagePath1, string imagePath2, bool preferColor);

    #region CalculateSimilarity

    [Fact]
    public virtual void CalculateSimilarity_IdenticalImages_ReturnsNearOne()
    {
        var score = CalculateSimilarity(Images.ColorfulA, Images.ColorfulACopy);
        Assert.True(score >= 0.99f, $"Expected near-1 similarity for identical images, got {score}");
    }

    [Fact]
    public virtual void CalculateSimilarity_ColorVsGrayscale_ReturnsLessThanOne()
    {
        var score = CalculateSimilarity(Images.ColorfulA, Images.Grayscale);
        Assert.True(score < 1.0f, $"Expected <1 similarity for differing images, got {score}");
    }

    [Fact]
    public virtual void CalculateSimilarity_ScoreWithinUnitRange()
    {
        var score = CalculateSimilarity(Images.HighRes, Images.LowRes);
        Assert.InRange(score, 0f, 1f);
    }

    [Fact]
    public virtual void CalculateSimilarity_MissingFile_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => CalculateSimilarity(Images.ColorfulA, Images.Missing));
    }

    #endregion

    #region GetBetterImage

    [Fact]
    public virtual void GetBetterImage_MuchHigherResolution_PicksHigherRes()
    {
        // 600x600 (360k px) vs 100x100 (10k px) trips the "3x or more" short-circuit.
        Assert.Equal(Images.HighRes, GetBetterImage(Images.HighRes, Images.LowRes, true));
        Assert.Equal(Images.HighRes, GetBetterImage(Images.LowRes, Images.HighRes, true));
    }

    [Fact]
    public virtual void GetBetterImage_PrefersColorOverGrayscale_WhenPreferColor()
    {
        // Same resolution, so the decision falls through to the color-preference branch.
        Assert.Equal(Images.ColorfulA, GetBetterImage(Images.ColorfulA, Images.Grayscale, true));
        Assert.Equal(Images.ColorfulA, GetBetterImage(Images.Grayscale, Images.ColorfulA, true));
    }

    [Fact]
    public virtual void GetBetterImage_ReturnsOneOfTheInputs()
    {
        var better = GetBetterImage(Images.ColorfulA, Images.LowRes, true);
        Assert.True(better == Images.ColorfulA || better == Images.LowRes,
            $"Expected one of the two inputs, got {better}");
    }

    [Fact]
    public virtual void GetBetterImage_MissingFile_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => GetBetterImage(Images.ColorfulA, Images.Missing, true));
    }

    #endregion
}
