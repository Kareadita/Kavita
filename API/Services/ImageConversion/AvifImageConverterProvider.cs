using Kavita.Common.EnvironmentInfo;
using Microsoft.Extensions.Logging;
using System.Threading;
using System;
using System.IO;
using NetVips;

namespace API.Services.ImageConversion
{
    /// <summary>
    /// Provides image conversion functionality for AVIF format using netvips library.
    /// </summary>
    public class AvifImageConverterProvider : IImageConverterProvider
    {
        private readonly ILogger<AvifImageConverterProvider> _logger;

        /// <summary>
        /// Gets a value indicating whether Vips supports AVIF.
        /// </summary>
        public bool IsVipsSupported => true;

        /// <summary>
        /// Initializes a new instance of the <see cref="AvifImageConverterProvider"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public AvifImageConverterProvider(ILogger<AvifImageConverterProvider> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Checks if the filename has the AVIF extension.
        /// </summary>
        /// <param name="filename">The filename of the image file.</param>
        /// <returns>True if the image is AVIF image type; otherwise, false.</returns>
        public bool IsSupported(string filename)
        {
            return filename.EndsWith(".avif", StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Converts the specified AVIF file to JPEG format.
        /// </summary>
        /// <param name="filename">The filename of the AVIF file.</param>
        /// <returns>The filename of the converted JPEG file.</returns>
        public string Convert(string filename)
        {
            string destination = Path.ChangeExtension(filename, "jpg");
            using var sourceImage = Image.NewFromFile(filename, false, Enums.Access.SequentialUnbuffered);
            sourceImage.WriteToFile(destination + "[Q=99]");
            File.Delete(filename);
            return destination;
        }

        /// <summary>
        /// Gets the dimensions (width and height) of the specified image file.
        /// </summary>
        /// <param name="fileName">The filename of the image file.</param>
        /// <returns>The dimensions of the image file as a tuple of width and height.</returns>
        public (int Width, int Height)? GetDimensions(string fileName)
        {
            using var image = Image.NewFromFile(fileName, memory: false, access: Enums.Access.SequentialUnbuffered);
            return (image.Width, image.Height);
        }
    }
}
