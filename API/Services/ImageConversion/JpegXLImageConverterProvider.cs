using System;
using System.IO;
using System.IO.Abstractions;
using System.Text.RegularExpressions;
using System.Threading;
using ImageMagick;
using Kavita.Common.EnvironmentInfo;
using Microsoft.Extensions.Logging;
using NetVips;

namespace API.Services.ImageConversion;

/// <summary>
/// Represents an image converter provider.
/// </summary>
public interface IImageConverterProvider
{
    /// <summary>
    /// Checks if the image converter supports the image format.
    /// </summary>
    /// <param name="filename">The filename of the image file.</param>
    /// <returns>True if the image converter supports the image type; otherwise, false.</returns>
    bool IsSupported(string filename);

    /// <summary>
    /// Converts the specified image to JPG.
    /// </summary>
    /// <param name="filename">The filename of the image file.</param>
    /// <returns>The converted image file.</returns>
    string Convert(string filename);

    /// <summary>
    /// Gets the dimensions of the specified image file.
    /// </summary>
    /// <param name="fileName">The filename of the image file.</param>
    /// <returns>The dimensions of the image file as a tuple of width and height, or null if the dimensions cannot be determined.</returns>
    (int Width, int Height)? GetDimensions(string fileName);

    /// <summary>
    /// Gets a value indicating whether the image type supports Vips.
    /// </summary>
    bool IsVipsSupported { get; }
}

public class ImageMagickConverterProvider
{
    /// <summary>
    /// Converts the specified image to JPG.
    /// </summary>
    /// <param name="filename">The filename of the image file.</param>
    /// <returns>The converted image file.</returns>
    public virtual string Convert(string filename)
    {
        string destination = Path.ChangeExtension(filename, "jpg");
        using var sourceImage = new MagickImage(filename);
        sourceImage.Quality = 99;
        sourceImage.Write(destination);
        File.Delete(filename);
        return destination;
    }

    /// <summary>
    /// Gets the dimensions of the specified image file.
    /// </summary>
    /// <param name="filename">The filename of the image file.</param>
    /// <returns>The dimensions of the image file as a tuple of width and height, or null if the dimensions cannot be determined.</returns>
    public virtual (int Width, int Height)? GetDimensions(string filename)
    {
        var info = new MagickImageInfo(filename);
        return (info.Width, info.Height);
    }
}

/// <summary>
/// Represents a JPEG-XL image converter provider.
/// </summary>
public class JpegXLImageConverterProvider : ImageMagickConverterProvider, IImageConverterProvider
{
    private readonly ILogger<JpegXLImageConverterProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JpegXLImageConverterProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public JpegXLImageConverterProvider(ILogger<JpegXLImageConverterProvider> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets a value indicating whether Vips supports JPEG-XL.
    /// </summary>
    public bool IsVipsSupported => false;

    /// <summary>
    /// Checks if the filename has the JPEG-XL extension.
    /// </summary>
    /// <param name="filename">The filename of the image file.</param>
    /// <returns>True if the image is JPEG-XL image type; otherwise, false.</returns>
    public bool IsSupported(string filename)
    {
        return filename.EndsWith(".jxl", StringComparison.InvariantCultureIgnoreCase);
    }
}
