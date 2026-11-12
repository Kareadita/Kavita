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
    /// Writes a cover thumbnail from an image file, logging and swallowing any error
    /// </summary>
    /// <param name="path">Image file to read</param>
    /// <param name="fileName">File name without extension</param>
    /// <param name="outputDirectory">Where to write the cover</param>
    /// <param name="encodeFormat">Convert and save as encoding format</param>
    /// <param name="size">Cover size to scale and crop to</param>
    /// <returns>File name with extension of the file, or empty string on failure</returns>
    string GetCoverImage(string path, string fileName, string outputDirectory, EncodeFormat encodeFormat, CoverImageSize size);

    /// <summary>
    /// Creates a Thumbnail version of a base64 image
    /// </summary>
    /// <param name="encodedImage">base64 encoded image</param>
    /// <param name="fileName">File name without extension</param>
    /// <param name="kind">Picks the size profile</param>
    /// <param name="policy">Encoding format and cover size</param>
    /// <param name="targetDirectory">If null, will write to <see cref="DirectoryService.CoverImageDirectory"/></param>
    /// <returns>File name with extension of the file, or empty string on failure</returns>
    string CreateThumbnailFromBase64(string encodedImage, string fileName, ImageKind kind, ImagePolicy policy, string? targetDirectory = null);

    /// <summary>
    /// Creates a thumbnail from an image file already on disk (e.g. one staged in the temp directory).
    /// </summary>
    /// <param name="sourceFile">Absolute path to the source image</param>
    /// <param name="fileName">File name without extension</param>
    /// <param name="kind">Picks the size profile</param>
    /// <param name="policy">Encoding format and cover size</param>
    /// <param name="targetDirectory">If null, will write to <see cref="DirectoryService.CoverImageDirectory"/></param>
    /// <returns>File name with extension of the file, or empty string on failure</returns>
    string CreateThumbnailFromFile(string sourceFile, string fileName, ImageKind kind, ImagePolicy policy, string? targetDirectory = null);
    /// <summary>
    /// Writes out a thumbnail by stream input
    /// </summary>
    /// <param name="stream">Stream to read; rewound first when it can seek</param>
    /// <param name="fileName">File name without extension</param>
    /// <param name="outputDirectory">Where to write the cover</param>
    /// <param name="encodeFormat">Convert and save as encoding format</param>
    /// <param name="size">Cover size to scale and crop to</param>
    /// <returns>File name with extension of the file</returns>
    string WriteCoverThumbnail(Stream stream, string fileName, string outputDirectory, EncodeFormat encodeFormat, CoverImageSize size = CoverImageSize.Default);
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
    void UpdateColorScape(IHasCoverImage entity);

    /// <summary>
    /// Downloads an image from the given URL, resizes it, and saves it to the covers directory.
    /// <remarks>Caller is responsible for validating the URL before calling this method (via <see cref="IUrlValidationService"/> ).</remarks>
    /// </summary>
    /// <param name="url">The URL to download the image from</param>
    /// <param name="fileName">Filename without extension</param>
    /// <param name="kind">Picks the size profile</param>
    /// <param name="policy">Encoding format and cover size</param>
    /// <returns>File name with extension of the saved file, or empty string on failure</returns>
    Task<string> CreateThumbnailFromUrl(string url, string fileName, ImageKind kind, ImagePolicy policy);
}
