using NetVips;

namespace Kavita.Models.Images;

/// <summary>
/// Represents some step in the render pipeline that transforms an Image
/// </summary>
/// <example><see cref="GreyscaleStep"/> -&gt; <see cref="FitToStep"/></example>
public interface IImageStep
{
    public Image Apply(Image image);
}
