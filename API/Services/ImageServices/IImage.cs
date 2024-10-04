using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using API.Entities.Enums;

namespace API.Services.ImageServices;

/// <summary>
/// Represents an image with various operations.
/// </summary>
public interface IImage : IDisposable
{
    /// <summary>
    /// Gets the width of the image.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the height of the image.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Creates a new instance of the image that is a copy of the current instance.
    /// </summary>
    /// <returns>A new instance of the image.</returns>
    IImage Clone();

    /// <summary>
    /// Resizes the image to the specified width and height.
    /// </summary>
    /// <param name="width">The new width of the image.</param>
    /// <param name="height">The new height of the image.</param>
    void Resize(int width, int height);

    /// <summary>
    /// Crops the image to the specified region.
    /// </summary>
    /// <param name="x">The x-coordinate of the top-left corner of the region.</param>
    /// <param name="y">The y-coordinate of the top-left corner of the region.</param>
    /// <param name="width">The width of the region.</param>
    /// <param name="height">The height of the region.</param>
    void Crop(int x, int y, int width, int height);

    /// <summary>
    /// Creates a thumbnail of the image with the specified width and height.
    /// </summary>
    /// <param name="width">The width of the thumbnail.</param>
    /// <param name="height">The height of the thumbnail.</param>
    void Thumbnail(int width, int height);

    /// <summary>
    /// Overlays another image onto the current image at the specified position.
    /// </summary>
    /// <param name="overlay">The image to overlay.</param>
    /// <param name="x">The x-coordinate of the top-left corner of the overlay.</param>
    /// <param name="y">The y-coordinate of the top-left corner of the overlay.</param>
    void Composite(IImage overlay, int x, int y);

    /// <summary>
    /// Saves the image to the specified file with the specified format and quality.
    /// </summary>
    /// <param name="filename">The name of the file to save the image to.</param>
    /// <param name="format">The format to save the image in.</param>
    /// <param name="quality">The quality of the saved image.</param>
    void Save(string filename, EncodeFormat format, int quality);

    /// <summary>
    /// Saves the image to the specified stream with the specified format and quality.
    /// </summary>
    /// <param name="stream">The stream to save the image to.</param>
    /// <param name="format">The format to save the image in.</param>
    /// <param name="quality">The quality of the saved image.</param>
    void Save(Stream stream, EncodeFormat format, int quality);

    /// <summary>
    /// Asynchronously saves the image to the specified file with the specified format and quality.
    /// </summary>
    /// <param name="filename">The name of the file to save the image to.</param>
    /// <param name="format">The format to save the image in.</param>
    /// <param name="quality">The quality of the saved image.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>A task representing the asynchronous save operation.</returns>
    Task SaveAsync(string filename, EncodeFormat format, int quality, CancellationToken token = default);

    /// <summary>
    /// Asynchronously saves the image to the specified stream with the specified format and quality.
    /// </summary>
    /// <param name="stream">The stream to save the image to.</param>
    /// <param name="format">The format to save the image in.</param>
    /// <param name="quality">The quality of the saved image.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>A task representing the asynchronous save operation.</returns>
    Task SaveAsync(Stream stream, EncodeFormat format, int quality, CancellationToken token = default);

    /// <summary>
    /// Gets the RGBA image data as an array of floats.
    /// </summary>
    /// <returns>An array of floats representing the RGBA image data.</returns>
    float[] GetRGBAImageData();
}
