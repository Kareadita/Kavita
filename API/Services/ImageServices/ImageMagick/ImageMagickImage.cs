using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using API.Entities.Enums;
using ImageMagick;

namespace API.Services.ImageServices.ImageMagick;

/// <summary>
/// Represents an image using ImageMagick library.
/// </summary>
public class ImageMagickImage : IImage
{
    private MagickImage _image;

    /// <inheritdoc/>
    public int Width => _image?.Width ?? 0;

    /// <inheritdoc/>
    public int Height => _image?.Height ?? 0;

    /// <summary>
    /// Creates an instance of <see cref="ImageMagickImage"/> from a base64 string.
    /// </summary>
    /// <param name="base64">The base64 string representing the image.</param>
    /// <returns>An instance of <see cref="ImageMagickImage"/>.</returns>
    public static IImage CreateFromBase64(string base64)
    {
        ImageMagickImage m = new ImageMagickImage();
        m._image = (MagickImage)MagickImage.FromBase64(base64);
        return m;
    }

    /// <summary>
    /// Creates an instance of <see cref="ImageMagickImage"/> from a BGRA byte array.
    /// </summary>
    /// <param name="bgraByteArray">The BGRA byte array.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <returns>An instance of <see cref="ImageMagickImage"/>.</returns>
    public static IImage CreateFromBGRAByteArray(byte[] bgraByteArray, int width, int height)
    {
        //Convert to RGBA float array (Image Magick 16 uses float array with values from 0-65535)
        var floats = new float[bgraByteArray.Length];
        for (var i = 0; i < bgraByteArray.Length; i += 4)
        {
            floats[i] = bgraByteArray[i + 2] << 8;
            floats[i + 1] = bgraByteArray[i + 1] << 8;
            floats[i + 2] = bgraByteArray[i] << 8;
            floats[i + 3] = bgraByteArray[i + 3] << 8;
        }
        ImageMagickImage m = new ImageMagickImage();
        m._image = new MagickImage(MagickColor.FromRgba(0, 0, 0, 0), width, height);
        using var pixels = m._image.GetPixels();
        pixels.SetArea(0, 0, width, height, floats);
        return m;
    }

    internal ImageMagickImage()
    {
    }

    /// <summary>
    /// Creates an instance of <see cref="ImageMagickImage"/> from a file.
    /// </summary>
    /// <param name="filename">The path to the file.</param>
    public ImageMagickImage(string filename)
    {
        _image = new MagickImage(filename);
    }

    /// <summary>
    /// Creates an instance of <see cref="ImageMagickImage"/> from a stream.
    /// </summary>
    /// <param name="stream">The stream containing the image data.</param>
    public ImageMagickImage(Stream stream)
    {
        _image = new MagickImage(stream);
    }

    /// <summary>
    /// Creates an instance of <see cref="ImageMagickImage"/> from an existing <see cref="MagickImage"/>.
    /// </summary>
    /// <param name="image">The existing <see cref="MagickImage"/>.</param>
    public ImageMagickImage(MagickImage image)
    {
        _image = (MagickImage)image.Clone();
    }

    /// <summary>
    /// Creates an instance of <see cref="ImageMagickImage"/> with the specified width, height, and color.
    /// </summary>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="red">The red component of the color.</param>
    /// <param name="green">The green component of the color.</param>
    /// <param name="blue">The blue component of the color.</param>
    public ImageMagickImage(int width, int height, byte red = 0, byte green = 0, byte blue = 0)
    {
        _image = new MagickImage(MagickColor.FromRgb(red, green, blue), width, height);
    }

    /// <inheritdoc/>
    public IImage Clone()
    {
        return new ImageMagickImage(_image);
    }

    /// <inheritdoc/>
    public void Resize(int width, int height)
    {
        _image.Resize(width, height);
    }

    /// <inheritdoc/>
    public void Crop(int x, int y, int width, int height)
    {
        _image.Crop(new MagickGeometry(x, y, width, height));
    }

    /// <inheritdoc/>
    public void Thumbnail(int width, int height)
    {
        _image.Thumbnail(new MagickGeometry(width, height) { IgnoreAspectRatio = true });
    }

    /// <inheritdoc/>
    public void Composite(IImage overlay, int x, int y)
    {
        ImageMagickImage tile = overlay as ImageMagickImage;
        if (tile == null) return;
        _image.Composite(tile._image, x, y, CompositeOperator.Over);
    }

    /// <inheritdoc/>
    public void Save(string filename, EncodeFormat format, int quality)
    {
        _image.Quality = quality;
        _image.Write(filename, MagickFormatFromEncodeFormat(format));
    }

    /// <inheritdoc/>
    public void Save(Stream stream, EncodeFormat format, int quality)
    {
        _image.Quality = quality;
        _image.Write(stream, MagickFormatFromEncodeFormat(format));
    }

    /// <inheritdoc/>
    public Task SaveAsync(string filename, EncodeFormat format, int quality, CancellationToken token = default)
    {
        _image.Quality = quality;
        return _image.WriteAsync(filename, MagickFormatFromEncodeFormat(format), token);
    }

    /// <inheritdoc/>
    public Task SaveAsync(Stream stream, EncodeFormat format, int quality, CancellationToken token = default)
    {
        _image.Quality = quality;
        return _image.WriteAsync(stream, MagickFormatFromEncodeFormat(format), token);
    }

    /// <inheritdoc/>
    public float[] GetRGBAImageData()
    {
        float[] data = null;
        float scale = 1.0f / 256;
        if (_image.ChannelCount == 4)
        {
            data = _image.GetPixels().GetValues();
            for (int x = 0; x < data.Length; x++)
            {
                data[x] *= scale;
            }
        }
        else if (_image.ChannelCount == 3)
        {
            float[] temp = _image.GetPixels().GetValues();
            data = new float[Width * Height * 4];
            int oi = 0;
            int ii = 0;
            for (int y = 0; y < Height * Width; y++)
            {

                data[oi++] = temp[ii++] * scale;
                data[oi++] = temp[ii++] * scale;
                data[oi++] = temp[ii++] * scale;
                data[oi++] = 255F;
            }
        }
        else if (_image.ChannelCount == 1)
        {
            float[] temp = _image.GetPixels().GetValues();
            data = new float[Width * Height * 4];
            int oi = 0;
            int ii = 0;
            for (int y = 0; y < Height * Width; y++)
            {
                data[oi++] = temp[ii++] * scale;
                data[oi++] = temp[oi - 1];
                data[oi++] = temp[oi - 1];
                data[oi++] = 255F;
            }
        }

        return data;
    }

    private static MagickFormat MagickFormatFromEncodeFormat(EncodeFormat format)
    {
        return format switch
        {
            EncodeFormat.PNG => MagickFormat.Png32,
            EncodeFormat.WEBP => MagickFormat.WebP,
            EncodeFormat.AVIF => MagickFormat.Avif,
            EncodeFormat.JPEG => MagickFormat.Jpeg,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _image?.Dispose();
    }
}
