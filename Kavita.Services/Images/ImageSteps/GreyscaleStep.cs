using Kavita.Models.Images;
using NetVips;

namespace Kavita.Services.Images.ImageSteps;

/// <summary>
/// Converts the image to Greyscale
/// </summary>
public record GreyscaleStep() : IImageStep
{
    public Image Apply(Image image)
    {
        return image.Colourspace(Enums.Interpretation.Bw);
    }
}
