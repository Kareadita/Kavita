using Microsoft.Extensions.Logging;
using NetVips;
using System.IO;
using System;
using ImageMagick;

namespace API.Services.ImageConversion
{
    public class HeifImageConverterProvider : ImageMagickConverterProvider, IImageConverterProvider
    {
        private readonly ILogger<HeifImageConverterProvider> _logger;
        public bool IsVipsSupported => false;

        public HeifImageConverterProvider(ILogger<HeifImageConverterProvider> logger)
        {
            _logger = logger;
        }

        public bool IsSupported(string filename)
        {
            return filename.EndsWith(".heif", StringComparison.InvariantCultureIgnoreCase)
                   || filename.EndsWith(".heic", StringComparison.InvariantCultureIgnoreCase);

        }

    }
}
