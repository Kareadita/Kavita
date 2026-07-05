using Kavita.Imaging.Tests.Backends.NetVips;

namespace Kavita.Imaging.Tests;

/// <summary>
/// Runs the shared behaviour suite against the in-progress NetVips implementation.
///
/// Every case is [Skip]ped while <see cref="Backends.NetVips.ImageExtensions"/> throws
/// NotImplementedException. As you migrate a method, delete the matching Skip below and the
/// test runs against the same assertions the ImageSharp oracle already passes. When all
/// Skips are gone and green, NetVips has parity.
///
/// Skip groups:
///  - CalculateSimilarity* gate on ImageExtensions.CalculateSimilarity
///  - GetBetterImage*       gate on ImageExtensions.GetBetterImage
/// </summary>
public class NetVipsImageExtensionsTests : ImageExtensionsTestsBase
{
    private const string SimilarityPending = "NetVips CalculateSimilarity migration pending";
    private const string BetterImagePending = "NetVips GetBetterImage migration pending";

    public NetVipsImageExtensionsTests(ImageFixture images) : base(images) { }

    protected override float CalculateSimilarity(string imagePath1, string imagePath2)
        => imagePath1.CalculateSimilarity(imagePath2);

    protected override string GetBetterImage(string imagePath1, string imagePath2, bool preferColor)
        => imagePath1.GetBetterImage(imagePath2, preferColor);

    #region CalculateSimilarity

    [Fact(Skip = SimilarityPending)]
    public override void CalculateSimilarity_IdenticalImages_ReturnsNearOne()
        => base.CalculateSimilarity_IdenticalImages_ReturnsNearOne();

    [Fact(Skip = SimilarityPending)]
    public override void CalculateSimilarity_ColorVsGrayscale_ReturnsLessThanOne()
        => base.CalculateSimilarity_ColorVsGrayscale_ReturnsLessThanOne();

    [Fact(Skip = SimilarityPending)]
    public override void CalculateSimilarity_ScoreWithinUnitRange()
        => base.CalculateSimilarity_ScoreWithinUnitRange();

    [Fact(Skip = SimilarityPending)]
    public override void CalculateSimilarity_MissingFile_Throws()
        => base.CalculateSimilarity_MissingFile_Throws();

    #endregion

    #region GetBetterImage

    [Fact(Skip = BetterImagePending)]
    public override void GetBetterImage_MuchHigherResolution_PicksHigherRes()
        => base.GetBetterImage_MuchHigherResolution_PicksHigherRes();

    [Fact(Skip = BetterImagePending)]
    public override void GetBetterImage_PrefersColorOverGrayscale_WhenPreferColor()
        => base.GetBetterImage_PrefersColorOverGrayscale_WhenPreferColor();

    [Fact(Skip = BetterImagePending)]
    public override void GetBetterImage_ReturnsOneOfTheInputs()
        => base.GetBetterImage_ReturnsOneOfTheInputs();

    [Fact(Skip = BetterImagePending)]
    public override void GetBetterImage_MissingFile_Throws()
        => base.GetBetterImage_MissingFile_Throws();

    #endregion
}
