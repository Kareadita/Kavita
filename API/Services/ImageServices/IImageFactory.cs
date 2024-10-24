using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace API.Services.ImageServices;

/// <summary>
/// Represents a factory for creating images.
/// </summary>
public interface IImageFactory
{
    /// <summary>
    /// Creates an image from the specified file.
    /// </summary>
    /// <param name="filename">The path to the image file.</param>
    /// <returns>The created image.</returns>
    IImage Create(string filename);

    /// <summary>
    /// Creates an image from the specified stream.
    /// </summary>
    /// <param name="stream">The stream containing the image data.</param>
    /// <returns>The created image.</returns>
    IImage Create(Stream stream);

    /// <summary>
    /// Creates a blank image with the specified dimensions and color.
    /// </summary>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="red">The red component of the color (default is 0).</param>
    /// <param name="green">The green component of the color (default is 0).</param>
    /// <param name="blue">The blue component of the color (default is 0).</param>
    /// <returns>The created image.</returns>
    IImage Create(int width, int height, byte red = 0, byte green = 0, byte blue = 0);

    /// <summary>
    /// Creates an image from the specified base64 string.
    /// </summary>
    /// <param name="base64">The base64 string representing the image data.</param>
    /// <returns>The created image.</returns>
    IImage CreateFromBase64(string base64);

    /// <summary>
    /// Creates an image from the specified BGRA byte array (Output from a pdf page)
    /// </summary>
    /// <param name="bgraByteArray">The BGRA byte array representing the image data.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <returns>The created image.</returns>
    IImage CreateFromBGRAByteArray(byte[] bgraByteArray, int width, int height);

    /// <summary>
    /// Gets the dimensions (width and height) of the specified image file.
    /// </summary>
    /// <param name="filename">The path to the image file.</param>
    /// <returns>The dimensions of the image file, or null if the dimensions cannot be determined.</returns>
    (int Width, int Height)? GetDimensions(string filename);

    /// <summary>
    /// Gets the RGB pixels of the specified image file that is resized to a certain percentage.
    /// </summary>
    /// <param name="filename">The path to the image file.</param>
    /// <param name="percent">The resizing percentage.</param>
    /// <returns>A list of RGB pixels.</returns>
    List<Vector3> GetRgbPixelsPercentage(string filename, float percent);
}
