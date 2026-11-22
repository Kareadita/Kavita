using Kavita.Models.Images;
using NetVips;

namespace Kavita.Services.Images.ImageSteps;

/// <summary>
/// Converts the image to Greyscale
/// </summary>
public sealed record GreyscaleStep : IImageStep
{
    public Image Apply(Image image)
    {
        // TODO: This has a banding problem
        return image.Colourspace(Enums.Interpretation.Bw);
    }
}
