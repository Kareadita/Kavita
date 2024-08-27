using Kavita.Common.EnvironmentInfo;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System;

namespace API.Services.ImageConversion
{
    /// <summary>
    /// Provides image conversion functionality for JPEG2000 images.
    /// </summary>
    public class Jpeg2000ImageConverterProvider : ImageMagickConverterProvider, IImageConverterProvider
    {
        private readonly ILogger<Jpeg2000ImageConverterProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="Jpeg2000ImageConverterProvider"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public Jpeg2000ImageConverterProvider(ILogger<Jpeg2000ImageConverterProvider> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Gets a value indicating whether Vips supports JPEG 2000.
        /// </summary>
        public bool IsVipsSupported => false;

        /// <summary>
        /// Checks if the filename has the JPEG 2000 extension.
        /// </summary>
        /// <param name="filename">The filename of the image file.</param>
        /// <returns>True if the image is JPEG 2000 image type; otherwise, false.</returns>
        public bool IsSupported(string filename)
        {
            return filename.EndsWith(".jp2", StringComparison.InvariantCultureIgnoreCase) || filename.EndsWith(".j2k", StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
