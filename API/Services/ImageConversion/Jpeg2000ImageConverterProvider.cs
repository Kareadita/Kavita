using Kavita.Common.EnvironmentInfo;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System;

namespace API.Services.ImageConversion
{
    //NetVips do not support Jpeg2000, Vips does but generate the right bindings is a pain in the ass
    public class Jpeg2000ImageConverterProvider : ImageMagickConverterProvider, IImageConverterProvider
    {
        private readonly ILogger<Jpeg2000ImageConverterProvider> _logger;
        public Jpeg2000ImageConverterProvider(ILogger<Jpeg2000ImageConverterProvider> logger)
        {
            _logger = logger;
        }

        public bool IsVipsSupported => false;
        public bool IsSupported(string filename)
        {
            return filename.EndsWith(".jp2", StringComparison.InvariantCultureIgnoreCase) || filename.EndsWith(".j2k", StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
