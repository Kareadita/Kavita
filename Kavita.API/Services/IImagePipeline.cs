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
    /// Copies <paramref name="sourcePath"/> byte for byte to <paramref name="fileName"/> in <paramref name="directory"/>.
    /// The caller picks the extension; it should match the content, since the bytes are not re-encoded
    /// </summary>
    ImageWriteResult Copy(string sourcePath, ImageKind kind, string directory, string fileName);

    /// <summary>
    /// Encodes <paramref name="sourcePath"/> again under <paramref name="policy"/> into <paramref name="fileName"/> in
    /// <paramref name="directory"/>, shrinking it into the kind's box. Never enlarges and never crops, unlike <see cref="Write"/>
    /// </summary>
    ImageWriteResult Reencode(string sourcePath, ImageKind kind, ImagePolicy policy, string directory, string fileName);

    /// <inheritdoc cref="Reencode(string, ImageKind, ImagePolicy, string, string)"/>
    ImageWriteResult Reencode(byte[] sourceBytes, ImageKind kind, ImagePolicy policy, string directory, string fileName);
}
