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
    /// Writes out a thumbnail by file path input
    /// </summary>
    /// <param name="sourceFile">Image file to read</param>
    /// <param name="fileName">File name without extension</param>
    /// <param name="outputDirectory">Where to write the cover</param>
    /// <param name="encodeFormat">Convert and save as encoding format</param>
    /// <param name="size">Cover size to scale and crop to</param>
    /// <returns>File name with extension of the file</returns>
    string WriteCoverThumbnail(string sourceFile, string fileName, string outputDirectory, EncodeFormat encodeFormat, CoverImageSize size = CoverImageSize.Default);

    /// <summary>
    /// Converts the passed image to encoding and outputs it in the same directory
    /// </summary>
    /// <param name="filePath">Full path to the image to convert</param>
    /// <param name="outputPath">Where to output the file</param>
    /// <param name="encodeFormat">Encoding Format</param>
    /// <param name="ct"></param>
    /// <returns>File of written encoded image</returns>
    Task<string> ConvertToEncodingFormat(string filePath, string outputPath, EncodeFormat encodeFormat, CancellationToken ct = default);

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
