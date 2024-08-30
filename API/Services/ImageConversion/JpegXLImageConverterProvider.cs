using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Runtime.InteropServices;
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

    /// <summary>
    /// Creates a NetVips Image object from the specified image stream.
    /// </summary>
    /// <param name="source">The source image stream.</param>
    /// <returns>The NetVips Image object created from the image stream.</returns>
    Image ImageFromStream(Stream source);
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
    /// Creates a NetVips Image object from the specified image stream.
    /// </summary>
    /// <param name="source">The source image stream.</param>
    /// <returns>The NetVips Image object created from the image stream.</returns>
    public virtual Image ImageFromStream(Stream source)
    {
        var settings = new MagickReadSettings
        {
            ColorSpace = ColorSpace.sRGB
        };
        using var sourceImage = new MagickImage(source, settings);
        float[] pixels = sourceImage.GetPixels().ToArray();
        float mul = 1F / 255F;
        for (int x = 0; x < pixels.Length; x++)
            pixels[x] *= mul;
        GCHandle handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        IntPtr pointer = handle.AddrOfPinnedObject();
        ulong size = (ulong)(Marshal.SizeOf<float>() * pixels.Length);
        Image im = Image.NewFromMemoryCopy(pointer, size, sourceImage.Width, sourceImage.Height,3, Enums.BandFormat.Float);
        handle.Free();
        return im;
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
