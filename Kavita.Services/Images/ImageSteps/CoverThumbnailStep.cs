using Kavita.Models.Images;
using NetVips;

namespace Kavita.Services.Images.ImageSteps;

/// <summary>
/// Frames an image as a cover of exactly <paramref name="Width"/> x <paramref name="Height"/>: scales it to fill the box,
/// cropping to the most interesting part when the shapes differ. The only step that can enlarge
/// </summary>
/// <param name="Width">Cover width, from the policy's <c>CoverSize</c> (320 for Default)</param>
/// <param name="Height">Cover height, from the policy's <c>CoverSize</c> (455 for Default)</param>
public sealed record CoverThumbnailStep(int Width, int Height) : IResizeStep
{
    public ThumbnailTarget TargetFor(int sourceWidth, int sourceHeight)
    {
        return new ThumbnailTarget(Width, Height,
            ImagePipeline.GetSizeForDimensions(sourceWidth, sourceHeight, Width, Height),
            ImagePipeline.GetCropForDimensions(sourceWidth, sourceHeight, Width, Height));
    }

    public Image Apply(Image image)
    {
        var target = TargetFor(image.Width, image.Height);
        return image.ThumbnailImage(target.Width, height: target.Height, size: target.Size, crop: target.Crop);
    }
}
