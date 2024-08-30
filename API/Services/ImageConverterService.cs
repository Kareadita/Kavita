using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using API.Services.ImageConversion;
using Microsoft.Extensions.Logging;
using NetVips;

namespace API.Services
{
    /// <summary>
    /// Represents an image converter service.
    /// </summary>
    public interface IImageConverterService
    {
        /// <summary>
        /// Converts the input stream into a Vips Image.
        /// </summary>
        /// <param name="originalNameWithExtension">The OriginalNameWithExtension of the image
        /// (Not a file,  only the name, if it came from the filesystem or an archive entry to identify the image type).</param>
        /// <param name="source">The input stream of the image.</param>
        /// <returns>NetVips Image from the input Stream.</returns>
        Image GetImageFromStream(string originalNameWithExtension, Stream source);

        /// <summary>
        /// Converts the specified image file to jpg image format.
        /// </summary>
        /// <param name="filename">The filename of the image.</param>
        /// <param name="supportedImageFormats">The list of supported image formats by the client from the Accept Header. If null is passed, the conversion will execute if the image requires conversion.</param>
        /// <returns>The converted image file path if conversion is required. Note: original filename is deleted if conversion is executed</returns>
        string ConvertFile(string filename, List<string> supportedImageFormats = null);

        /// <summary>
        /// Gets the dimensions (width and height) of the specified image file without read the whole image.
        /// </summary>
        /// <param name="fileName">The filename of the image.</param>
        /// <returns>The dimensions of the image (width and height).</returns>
        (int Width, int Height)? GetDimensions(string fileName);

        /// <summary>
        /// Checks if the VIPS library supports for the specified image type.
        /// </summary>
        /// <param name="filename">The filename of the image.</param>
        /// <returns>True if the VIPS library support it; otherwise, false.</returns>
        bool IsVipsSupported(string filename);
    }

    /// <summary>
    /// Represents an implementation of the image converter service.
    /// </summary>
    public class ImageConverterService : IImageConverterService
    {
        private readonly IEnumerable<IImageConverterProvider> _converters;
        private readonly ILogger<ImageConverterService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageConverterService"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="converters">The collection of image converter providers.</param>
        public ImageConverterService(ILogger<ImageConverterService> logger, IEnumerable<IImageConverterProvider> converters)
        {
            _logger = logger;
            _converters = converters;
        }

        /// <inheritdoc/>
        public Image GetImageFromStream(string originalNameWithExtension, Stream source)
        {
            //Check if the name have one of our supported extensions.
            IImageConverterProvider provider = _converters.FirstOrDefault(a => a.IsSupported(originalNameWithExtension));

            if (provider == null || provider.IsVipsSupported) return Image.NewFromStream(source); //No need to transform, Vips supports the format.

            var sw = Stopwatch.StartNew();
            Image image = provider.ImageFromStream(source);
            _logger.LogDebug("Image converted from '{Extension}' to '.jpg' in {ElapsedMilliseconds} milliseconds", Path.GetExtension(originalNameWithExtension), sw.ElapsedMilliseconds);
            return image;
        }

        private bool CheckDirectSupport(string filename, List<string> supportedImageFormats)
        {
            if (supportedImageFormats == null)
                return false;
            string ext = Path.GetExtension(filename).ToLowerInvariant().Substring(1);
            return supportedImageFormats.Contains(ext);
        }

        /// <inheritdoc/>
        public string ConvertFile(string filename, List<string> supportedImageFormats = null)
        {
            if (CheckDirectSupport(filename, supportedImageFormats))
                return filename;
            IImageConverterProvider provider = _converters.FirstOrDefault(a => a.IsSupported(filename));
            if (provider == null) //No provider for this image type, so, is a common image format.
                return filename;
            var sw = Stopwatch.StartNew();
            filename = provider.Convert(filename);
            _logger.LogDebug("Image converted from '{Extension}' to '.jpg' in {ElapsedMilliseconds} milliseconds", Path.GetExtension(filename), sw.ElapsedMilliseconds);
            return filename;
        }

        /// <inheritdoc/>
        public (int Width, int Height)? GetDimensions(string fileName)
        {
            //Provider supported
            IImageConverterProvider provider = _converters.FirstOrDefault(a => a.IsSupported(fileName));
            if (provider != null)
                return provider.GetDimensions(fileName);

            //No provider for this image type, so, is a common image format, use netvips and original code.
            using var image = Image.NewFromFile(fileName, memory: false, access: Enums.Access.SequentialUnbuffered);
            return (image.Width, image.Height);
        }

        /// <inheritdoc/>
        public bool IsVipsSupported(string filename)
        {
            IImageConverterProvider provider = _converters.FirstOrDefault(a => a.IsSupported(filename));
            if (provider == null)
                return true;
            return provider.IsVipsSupported;
        }
    }
}
