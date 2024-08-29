using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using API.Services.ImageConversion;
using NetVips;

namespace API.Services
{
    /// <summary>
    /// Represents an image converter service.
    /// </summary>
    public interface IImageConverterService
    {
        /// <summary>
        /// Converts the input stream to jpg image format.
        /// </summary>
        /// <param name="originalNameWithExtension">The OriginalNameWithExtension of the image
        /// (Not a file,  only the name, if it came from the filesystem or an archive entry).</param>
        /// <param name="source">The input stream of the image.</param>
        /// <returns>The converted image stream.</returns>
        Stream ConvertStream(string originalNameWithExtension, Stream source);

        /// <summary>
        /// Converts the specified image file to jpg image format.
        /// </summary>
        /// <param name="filename">The filename of the image.</param>
        /// <param name="supportedImageFormats">The list of supported image formats by the client from the Accept Header.</param>
        /// <returns>The converted image file path if conversion is required.</returns>
        string ConvertFile(string filename, List<string> supportedImageFormats);

        /// <summary>
        /// Gets the dimensions (width and height) of the specified image file.
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
        private IEnumerable<IImageConverterProvider> _converters;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageConverterService"/> class.
        /// </summary>
        /// <param name="converters">The collection of image converter providers.</param>
        public ImageConverterService(IEnumerable<IImageConverterProvider> converters)
        {
            _converters = converters;
        }

        /// <inheritdoc/>
        public Stream ConvertStream(string originalNameWithExtension, Stream source)
        {
            //Check if the name have one of our supported extensions.
            IImageConverterProvider provider = _converters.FirstOrDefault(a => a.IsSupported(originalNameWithExtension));
            if (provider == null)
                return source;
            //We support it, create a temp file, and write the original content, so it can be transcoded into another stream.
            string tempFileName = Path.ChangeExtension(Path.GetTempFileName(), Path.GetExtension(originalNameWithExtension));
            Stream dest = File.OpenWrite(tempFileName);
            source.CopyTo(dest);
            source.Close();
            dest.Close();
            return File.OpenRead(provider.Convert(tempFileName));
        }

        private bool CheckDirectSupport(string filename, List<string> supportedImageFormats)
        {
            if (supportedImageFormats == null)
                return false;
            string ext = Path.GetExtension(filename).ToLowerInvariant().Substring(1);
            return supportedImageFormats.Contains(ext);
        }

        /// <inheritdoc/>
        public string ConvertFile(string filename, List<string> supportedImageFormats)
        {
            if (CheckDirectSupport(filename, supportedImageFormats))
                return filename;
            IImageConverterProvider provider = _converters.FirstOrDefault(a => a.IsSupported(filename));
            if (provider == null)
                return filename;
            return provider.Convert(filename);
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
