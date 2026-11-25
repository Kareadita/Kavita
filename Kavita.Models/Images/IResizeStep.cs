namespace Kavita.Models.Images;

/// <summary>
/// Any IImageStep that has this performs some resizing implementation. This allows for decoding optimizations in NetVips.
/// </summary>
public interface IResizeStep : IImageStep
{
    ThumbnailTarget TargetFor(int sourceWidth, int sourceHeight);
}
