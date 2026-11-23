using Kavita.Models.Images;
using NetVips;

namespace Kavita.Services.Images.ImageSteps;

/// <summary>
/// Scales the image to the largest size that fits within the box, keeping its shape. Unlike <see cref="FitWithinStep"/>,
/// a smaller image is enlarged
/// </summary>
public sealed record FitToStep(int Width, int Height) : IResizeStep
{
    public ThumbnailTarget TargetFor(int sourceWidth, int sourceHeight)
    {
        return new ThumbnailTarget(Width, Height, Enums.Size.Both, null);
    }

    public Image Apply(Image image)
    {
        var target = TargetFor(image.Width, image.Height);
        return image.ThumbnailImage(target.Width, height: target.Height, size: target.Size, crop: target.Crop);
    }
}
