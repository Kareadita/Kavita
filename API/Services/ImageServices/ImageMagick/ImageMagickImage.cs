using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using API.Entities.Enums;
using API.Extensions;
using ImageMagick;

namespace API.Services.ImageServices.ImageMagick;

/// <summary>
/// Represents an image using ImageMagick library.
/// </summary>
public class ImageMagickImage : IImage
{
    private MagickImage _image;

    /// <inheritdoc/>
    public uint Width => _image?.Width ?? 0;

    /// <inheritdoc/>
    public uint Height => _image?.Height ?? 0;

    /// <summary>
    /// Creates an instance of <see cref="ImageMagickImage"/> from a base64 string.
    /// </summary>
    /// <param name="base64">The base64 string representing the image.</param>
    /// <returns>An instance of <see cref="ImageMagickImage"/>.</returns>
    public static IImage CreateFromBase64(string base64)
    {
        if (string.IsNullOrEmpty(base64))
            throw new ArgumentNullException(nameof(base64));

        ImageMagickImage m = new ImageMagickImage();
        m._image = (MagickImage) MagickImage.FromBase64(base64);
        return m;
    }

    /// <summary>
    /// Creates an instance of <see cref="ImageMagickImage"/> from a BGRA byte array.
    /// </summary>
    /// <param name="bgraByteArray">The BGRA byte array.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <returns>An instance of <see cref="ImageMagickImage"/>.</returns>
    public static IImage CreateFromBGRAByteArray(byte[] bgraByteArray, uint width, uint height)
    {
        // Convert to RGBA float array (Image Magick 16-HDRI uses float array with values from 0-65535)
        // Convert to RGBA float array (Image Magick 16 uses UInt16 array with values from 0-65535)
        // Convert to RGBA float array (Image Magick 8 uses byte array with values from 0-65535)
#if Q16HDRI
        var imageArray = new float[bgraByteArray.Length];
        for (var i = 0; i < bgraByteArray.Length; i += 4)
        {
            imageArray[i] = bgraByteArray[i + 2] << 8;
            imageArray[i + 1] = bgraByteArray[i + 1] << 8;
            imageArray[i + 2] = bgraByteArray[i] << 8;
            imageArray[i + 3] = bgraByteArray[i + 3] << 8;
        }
#elif Q16
        var imageArray = new System.UInt16[bgraByteArray.Length];
        for (var i = 0; i < bgraByteArray.Length; i += 4)
        {
            imageArray[i] = (ushort)(bgraByteArray[i + 2] << 8);
            imageArray[i + 1] = (ushort)(bgraByteArray[i + 1] << 8);
            imageArray[i + 2] = (ushort)(bgraByteArray[i] << 8);
            imageArray[i + 3] = (ushort)(bgraByteArray[i + 3] << 8);
        }
#else
        var imageArray = new byte[bgraByteArray.Length];
        for (var i = 0; i < bgraByteArray.Length; i += 4)
        {
            imageArray[i] = bgraByteArray[i + 2];
            imageArray[i + 1] = bgraByteArray[i + 1];
            imageArray[i + 2] = bgraByteArray[i];
            imageArray[i + 3] = bgraByteArray[i + 3];
        }
#endif
        ImageMagickImage m = new ImageMagickImage();
        m._image = new MagickImage(MagickColor.FromRgba(0, 0, 0, 0), width, height);
        using var pixels = m._image.GetPixels();
        pixels.SetArea(0, 0, width, height, imageArray);
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
        _image = (MagickImage) image.Clone();
    }

    /// <summary>
    /// Creates an instance of <see cref="ImageMagickImage"/> with the specified width, height, and color.
    /// </summary>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="red">The red component of the color.</param>
    /// <param name="green">The green component of the color.</param>
    /// <param name="blue">The blue component of the color.</param>
    public ImageMagickImage(uint width, uint height, byte red = 0, byte green = 0, byte blue = 0)
    {
        _image = new MagickImage(MagickColor.FromRgb(red, green, blue), width, height);
    }

    /// <inheritdoc/>
    public IImage Clone()
    {
        return new ImageMagickImage(_image);
    }

    /// <inheritdoc/>
    public void Resize(uint width, uint height)
    {
        _image.Resize(width, height);
    }

    /// <inheritdoc/>
    public void Crop(int x, int y, uint width, uint height)
    {
        _image.Crop(new MagickGeometry(x, y, width, height));
    }

    /// <inheritdoc/>
    public void Thumbnail(uint width, uint height)
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
    public void Save(string filename, EncodeFormat format)
    {
        _image.Quality = format.DefaultQuality();
        _image.Write(filename, MagickFormatFromEncodeFormat(format));
    }

    /// <inheritdoc/>
    public void Save(Stream stream, EncodeFormat format)
    {
        _image.Quality = format.DefaultQuality();
        _image.Write(stream, MagickFormatFromEncodeFormat(format));
    }

    /// <inheritdoc/>
    public Task SaveAsync(string filename, EncodeFormat format, CancellationToken token = default)
    {
        _image.Quality = format.DefaultQuality();
        return _image.WriteAsync(filename, MagickFormatFromEncodeFormat(format), token);
    }

    /// <inheritdoc/>
    public Task SaveAsync(Stream stream, EncodeFormat format, CancellationToken token = default)
    {
        _image.Quality = format.DefaultQuality();
        return _image.WriteAsync(stream, MagickFormatFromEncodeFormat(format), token);
    }

    public static float[] GetRGBAFloatImageDataFromImage(MagickImage image)
    {
        float[] data = [];
        uint height = image.Height;
        uint width = image.Width;
#if Q16HDRI
        float scale = 1.0f / 256;
        if (image.ChannelCount == 4)
        {
            data = image.GetPixels().GetValues();
            for (int x = 0; x < data.Length; x++)
            {
                data[x] *= scale;
            }
        }
        else if (image.ChannelCount == 3)
        {
            float[] temp = image.GetPixels().GetValues();
            data = new float[width * height * 4];
            int oi = 0;
            int ii = 0;
            for (int y = 0; y < height * width; y++)
            {

                data[oi++] = temp[ii++] * scale;
                data[oi++] = temp[ii++] * scale;
                data[oi++] = temp[ii++] * scale;
                data[oi++] = 255F;
            }
        }
        else if (image.ChannelCount == 1)
        {
            float[] temp = image.GetPixels().GetValues();
            data = new float[width * height * 4];
            int oi = 0;
            int ii = 0;
            for (int y = 0; y < height * width; y++)
            {
                data[oi++] = temp[ii++] * scale;
                data[oi++] = temp[oi - 1];
                data[oi++] = temp[oi - 1];
                data[oi++] = 255F;
            }
        }
#elif Q16
        if (image.ChannelCount == 4)
        {
            System.UInt16[] temp = image.GetPixels().GetValues();
            for (int x = 0; x < data.Length; x++)
            {
                data[x] = temp[x]>>8;
            }
        }
        else if (image.ChannelCount == 3)
        {
            
            System.UInt16[] temp = image.GetPixels().GetValues();
            data = new float[width * height * 4];
            int oi = 0;
            int ii = 0;
            for (int y = 0; y < height * width; y++)
            {

                data[oi++] = temp[ii++]>>8;
                data[oi++] = temp[ii++]>>8;
                data[oi++] = temp[ii++]>>8;
                data[oi++] = 255F;
            }
        }
        else if (image.ChannelCount == 1)
        {
            System.UInt16[] temp = image.GetPixels().GetValues();
            data = new float[width * height * 4];
            int oi = 0;
            int ii = 0;
            for (int y = 0; y < height * width; y++)
            {
                data[oi++] = temp[ii++]>>8;
                data[oi++] = temp[oi - 1];
                data[oi++] = temp[oi - 1];
                data[oi++] = 255F;
            }
        }
#else
        float scale = 1.0f / 256;
        byte[] original;
        if (image.ChannelCount == 4)
        {
            byte[] temp = image.GetPixels().GetValues();
            for (int x = 0; x < data.Length; x++)
            {
                data[x] = temp[x];
            }
        }
        else if (image.ChannelCount == 3)
        {
            byte[] temp = image.GetPixels().GetValues();
            data = new float[width * height * 4];
            int oi = 0;
            int ii = 0;
            for (int y = 0; y < height * width; y++)
            {

                data[oi++] = temp[ii++];
                data[oi++] = temp[ii++];
                data[oi++] = temp[ii++];
                data[oi++] = 255F;
            }
        }
        else if (image.ChannelCount == 1)
        {
            byte[] temp = image.GetPixels().GetValues();
            data = new float[width * height * 4];
            int oi = 0;
            int ii = 0;
            for (int y = 0; y < height * width; y++)
            {
                data[oi++] = temp[ii++];
                data[oi++] = temp[oi - 1];
                data[oi++] = temp[oi - 1];
                data[oi++] = 255F;
            }
        }
#endif
        return data;
    }

    /// <inheritdoc/>
    ///
    
    public float[] GetRGBAImageData()
    {
        return GetRGBAFloatImageDataFromImage(_image);
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
