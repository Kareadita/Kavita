using NetVips;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using API.Services.ImageConversion;
using static Org.BouncyCastle.Bcpg.Attr.ImageAttrib;

namespace API.Services
{
    public interface IImageConverterService
    {
        Stream ConvertStream(string filename, Stream source);
        string ConvertFile(string filename);
        void ConvertDirectory(string directory);
    }

    public class ImageConverterService : IImageConverterService
    {
        private IEnumerable<IImageConverterProvider> _converters;
        private readonly IDirectoryService _directoryService;
        public ImageConverterService(IDirectoryService directoryService, IEnumerable<IImageConverterProvider> converters)
        {
            _directoryService = directoryService;
            _converters = converters;
        }



        public Stream ConvertStream(string filename, Stream source)
        {
            IImageConverterProvider provider = _converters.FirstOrDefault(a => a.IsSupported(filename));
            if (provider == null)
                return source;
            string tempFile = Path.ChangeExtension(Path.GetFileName(filename), provider.Extension);
            Stream dest = File.OpenWrite(tempFile);
            source.CopyTo(dest);
            source.Close();
            dest.Close();
            return File.OpenRead(provider.Convert(tempFile));
        }

        public string ConvertFile(string filename)
        {
            IImageConverterProvider provider = _converters.FirstOrDefault(a => a.IsSupported(filename));
            if (provider == null)
                return filename;
            return provider.Convert(filename);
        }

        public void ConvertDirectory(string directory)
        {
            foreach (string filename in Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories))
            {
                IImageConverterProvider provider = _converters.FirstOrDefault(a => a.IsSupported(filename));
                if (provider != null)
                    provider.Convert(filename);
            }
        }
    }
}
