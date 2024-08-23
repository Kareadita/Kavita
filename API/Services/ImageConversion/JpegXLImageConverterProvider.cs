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

public interface IImageConverterProvider
{
    bool IsSupported(string filename);
    string Convert(string filename);
    (int Width, int Height)? GetDimensions(string fileName);

    bool IsVipsSupported { get; }
}

public class ImageMagickConverterProvider
{
    public virtual string Convert(string filename)
    {
        string destination = Path.ChangeExtension(filename, "jpg");
        using var sourceImage = new MagickImage(filename);
        sourceImage.Quality = 99;
        sourceImage.Write(destination);
        File.Delete(filename);
        return destination;
    }

    public virtual (int Width, int Height)? GetDimensions(string filename)
    {
        var info = new MagickImageInfo(filename);
        return (info.Width, info.Height);
    }
}

//NetVips do not support Jpeg-XL, Vips does but generate the right bindings is a pain in the ass
public class JpegXLImageConverterProvider : ImageMagickConverterProvider, IImageConverterProvider
{
    private readonly ILogger<JpegXLImageConverterProvider> _logger;
    public JpegXLImageConverterProvider(ILogger<JpegXLImageConverterProvider> logger)
    {
        _logger = logger;
    }
    public bool IsVipsSupported => false;

    public bool IsSupported(string filename)
    {
        return filename.EndsWith(".jxl", StringComparison.InvariantCultureIgnoreCase);
    }

  
}
