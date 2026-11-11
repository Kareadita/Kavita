using Kavita.Models.Images;

namespace Kavita.API.Services;

public interface IImagePipeline
{
    /// <summary>
    /// Decodes, sizes and encodes <paramref name="source"/> for <paramref name="kind"/>, then replaces
    /// <paramref name="fileName"/> in <paramref name="directory"/>
    /// </summary>
    ImageWriteResult Write(ImageSource source, ImageKind kind, ImagePolicy policy, string directory, string fileName);
}
