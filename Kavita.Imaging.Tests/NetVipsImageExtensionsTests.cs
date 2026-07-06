using Kavita.Imaging.Tests.Backends.NetVips;

namespace Kavita.Imaging.Tests;

/// <summary>
/// Runs the shared behaviour suite against the migrated NetVips implementation. Parity with the
/// frozen ImageSharp oracle is proven here (behaviour) and in <see cref="NetVipsParityTests"/>
/// (numeric closeness). When both are green, the NetVips <see cref="Backends.NetVips.ImageExtensions"/>
/// can be lifted back into Kavita.Common.
/// </summary>
public class NetVipsImageExtensionsTests : ImageExtensionsTestsBase
{
    public NetVipsImageExtensionsTests(ImageFixture images) : base(images) { }

    protected override float CalculateSimilarity(string imagePath1, string imagePath2)
        => imagePath1.CalculateSimilarity(imagePath2);

    protected override string GetBetterImage(string imagePath1, string imagePath2, bool preferColor)
        => imagePath1.GetBetterImage(imagePath2, preferColor);
}
