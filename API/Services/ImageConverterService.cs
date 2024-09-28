using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text.RegularExpressions;
using API.Services.Tasks.Scanner.Parser;
using ImageMagick;
using Microsoft.Extensions.Logging;


namespace API.Services
{
    /// <summary>
    /// Represents an image converter service.
    /// </summary>
    public interface IImageConverterService
    {

        /// <summary>
        /// Converts the specified image file to jpg image format.
        /// </summary>
        /// <param name="filename">The filename of the image.</param>
        /// <param name="supportedImageFormats">The list of supported image formats by the client from the Accept Header. If null is passed, the conversion will execute if the image requires conversion.</param>
        /// <returns>The converted image file path if conversion is required. Note: original filename is deleted if conversion is executed</returns>
        string ConvertFile(string filename, List<string> supportedImageFormats = null);

    }

    /// <summary>
    /// Represents an implementation of the image converter service.
    /// </summary>
    public class ImageConverterService : IImageConverterService
    {

        private readonly ILogger<ImageConverterService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageConverterService"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="converters">The collection of image converter providers.</param>
        public ImageConverterService(ILogger<ImageConverterService> logger)
        {
            _logger = logger;

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
            Match m = Regex.Match(Path.GetExtension(filename), Parser.NonUniversalFileImageExtensions, RegexOptions.IgnoreCase, Tasks.Scanner.Parser.Parser.RegexTimeout);
            if (!m.Success)
                return filename;
            var sw = Stopwatch.StartNew();
            string destination = Path.ChangeExtension(filename, "jpg");
            using var sourceImage = new MagickImage(filename);
            sourceImage.Quality = 99;
            sourceImage.Write(destination);
            File.Delete(filename);
            _logger.LogDebug("Image converted from '{Extension}' to '.jpg' in {ElapsedMilliseconds} milliseconds", Path.GetExtension(filename), sw.ElapsedMilliseconds);
            return destination;
        }




    }
}
