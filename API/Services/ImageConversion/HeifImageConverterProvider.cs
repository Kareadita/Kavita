using Microsoft.Extensions.Logging;
using NetVips;
using System.IO;
using System;
using ImageMagick;

namespace API.Services.ImageConversion
{
    /// <summary>
    /// Provides image conversion functionality for HEIF and HEIC file formats using ImageMagick.
    /// </summary>
    public class HeifImageConverterProvider : ImageMagickConverterProvider, IImageConverterProvider
    {
        private readonly ILogger<HeifImageConverterProvider> _logger;

        /// <summary>
        /// Gets a value indicating whether Vips supports HEIF.
        /// </summary>
        public bool IsVipsSupported => false;

        /// <summary>
        /// Initializes a new instance of the <see cref="HeifImageConverterProvider"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public HeifImageConverterProvider(ILogger<HeifImageConverterProvider> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Checks if the filename has the HEIF/HEIC extension.
        /// </summary>
        /// <param name="filename">The filename of the image file.</param>
        /// <returns>True if the image is HEIF/HEIC image type; otherwise, false.</returns>
        public bool IsSupported(string filename)
        {
            return filename.EndsWith(".heif", StringComparison.InvariantCultureIgnoreCase)
                   || filename.EndsWith(".heic", StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
