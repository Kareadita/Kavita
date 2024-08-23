using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using API.Services.ImageConversion;

namespace API.Services
{
    public interface IImageConverterService
    {
        Stream ConvertStream(string filename, Stream source);
        string ConvertFile(string filename, List<string> supportedImageFormats);
        (int Width, int Height)? GetDimensions(string fileName);

        public bool IsVipsSupported(string filename);
    }

    public class ImageConverterService : IImageConverterService
    {
        private IEnumerable<IImageConverterProvider> _converters;
        public ImageConverterService(IEnumerable<IImageConverterProvider> converters)
        {
            _converters = converters;
        }



        public Stream ConvertStream(string filename, Stream source)
        {
            IImageConverterProvider provider = _converters.FirstOrDefault(a => a.IsSupported(filename));
            if (provider == null)
                return source;
            string tempFile = Path.GetFileName(filename);
            Stream dest = File.OpenWrite(tempFile);
            source.CopyTo(dest);
            source.Close();
            dest.Close();
            return File.OpenRead(provider.Convert(tempFile));
        }

        private bool CheckDirectSupport(string filename, List<string> supportedImageFormats)
        {
            if (supportedImageFormats == null)
                return false;
            string ext = Path.GetExtension(filename).ToLowerInvariant().Substring(1);
            return supportedImageFormats.Contains(ext);
        }

        public string ConvertFile(string filename, List<string> supportedImageFormats)
        {
            if (CheckDirectSupport(filename, supportedImageFormats))
                return filename;
            IImageConverterProvider provider = _converters.FirstOrDefault(a => a.IsSupported(filename));
            if (provider == null)
                return filename;
            return provider.Convert(filename);
        }


        public (int Width, int Height)? GetDimensions(string fileName)
        {
            IImageConverterProvider provider = _converters.FirstOrDefault(a => a.IsSupported(fileName));
            if (provider == null)
                return null;
            return provider.GetDimensions(fileName);
        }

        public bool IsVipsSupported(string filename)
        {
            IImageConverterProvider provider = _converters.FirstOrDefault(a => a.IsSupported(filename));
            if (provider == null)
                return true;
            return provider.IsVipsSupported;
        }
    }
}
