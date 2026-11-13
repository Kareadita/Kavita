using Kavita.Models.Images;

namespace Kavita.API.Services;

public interface IImagePipeline
{
    /// <summary>
    /// Decodes, sizes and encodes <paramref name="source"/> for <paramref name="kind"/>, then replaces
    /// <paramref name="fileName"/> in <paramref name="directory"/>
    /// </summary>
    ImageWriteResult Write(ImageSource source, ImageKind kind, ImagePolicy policy, string directory, string fileName);

    /// <summary>
    /// Copies <paramref name="sourcePath"/> byte for byte to <paramref name="fileStem"/> in <paramref name="directory"/>.
    /// The extension comes from the source, not the encode setting, since the bytes are not re-encoded
    /// </summary>
    ImageWriteResult Copy(string sourcePath, ImageKind kind, string directory, string fileStem);
}
