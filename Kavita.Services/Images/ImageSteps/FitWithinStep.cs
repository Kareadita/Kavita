using Kavita.Models.Images;
using NetVips;

namespace Kavita.Services.Images.ImageSteps;

public sealed record FitWithinStep(int Width, int Height) : IResizeStep
{
    public ThumbnailTarget TargetFor(int sourceWidth, int sourceHeight)
    {
        return new ThumbnailTarget(Width, Height, Enums.Size.Down, null);
    }

    public Image Apply(Image image)
    {
        var target = TargetFor(image.Width, image.Height);
        return image.ThumbnailImage(target.Width, height: target.Height, size: target.Size, crop: target.Crop);
    }
}
