namespace Kavita.Models.Images;

/// <summary>
/// Where the image pipeline reads the source image from
/// </summary>
public abstract record ImageSource
{
    /// <summary>
    /// An image file on disk, read with shrink-on-load
    /// </summary>
    public sealed record FromFile(string Path) : ImageSource;

    /// <summary>
    /// An encoded image already in memory, such as a decoded base64 upload
    /// </summary>
    public sealed record FromBytes(byte[] Data) : ImageSource;

    /// <summary>
    /// The caller owns <paramref name="Stream"/> and disposes it
    /// </summary>
    public sealed record FromStream(System.IO.Stream Stream) : ImageSource;
}
