using Kavita.Common.EnvironmentInfo;
using Microsoft.Extensions.Logging;
using System.Threading;
using System;
using System.IO;
using NetVips;

namespace API.Services.ImageConversion
{
    //AVIF & HEIF are supported by netvips.

    public class AvifImageConverterProvider : IImageConverterProvider
    {
        private readonly ILogger<AvifImageConverterProvider> _logger;
        public bool IsVipsSupported => true;

        public AvifImageConverterProvider(ILogger<AvifImageConverterProvider> logger)
        {
            _logger = logger;
        }

        public bool IsSupported(string filename)
        {
            return filename.EndsWith(".avif", StringComparison.InvariantCultureIgnoreCase);

        }

        public string Convert(string filename)
        {
            string destination = Path.ChangeExtension(filename, "jpg");
            using var sourceImage = Image.NewFromFile(filename, false, Enums.Access.SequentialUnbuffered);
            sourceImage.WriteToFile(destination + "[Q=99]");
            File.Delete(filename);
            return destination;
        }

        public (int Width, int Height)? GetDimensions(string fileName)
        {
            using var image = Image.NewFromFile(fileName, memory: false, access: Enums.Access.SequentialUnbuffered);
            return (image.Width, image.Height);
        }

    }
}
