using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Interfaces;
using Kavita.Models.Images;

namespace Kavita.API.Services;

public interface IImageService
{
    void ExtractImages(string fileFilePath, string targetDirectory, int fileCount = 1);


    /// <summary>
    /// Reads the image format from the file header, never the file name
    /// </summary>
    /// <returns>The extension with its dot (<c>.png</c>, <c>.jpg</c>, <c>.webp</c>, <c>.gif</c>, <c>.avif</c>), or null
    /// when the file is not an image or is a format Kavita does not take for covers</returns>
    string? GetImageExtension(string filePath);
    /// <summary>
    /// <see cref="GetImageExtension"/> and the size, read from the header of an image already in memory
    /// </summary>
    /// <returns>Null when the bytes are not an image or are a format Kavita does not take for covers</returns>
    ImageHeader? GetImageHeader(byte[] bytes);
}
