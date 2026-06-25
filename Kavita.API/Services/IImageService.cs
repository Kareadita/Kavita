using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Interfaces;

namespace Kavita.API.Services;

public interface IImageService
{
    void ExtractImages(string fileFilePath, string targetDirectory, int fileCount = 1);
    string GetCoverImage(string path, string fileName, string outputDirectory, EncodeFormat encodeFormat, CoverImageSize size);

    /// <summary>
    /// Creates a Thumbnail version of a base64 image
    /// </summary>
    /// <param name="encodedImage">base64 encoded image</param>
    /// <param name="fileName"></param>
    /// <param name="encodeFormat">Convert and save as encoding format</param>
    /// <param name="thumbnailWidth">Width of thumbnail</param>
    /// <param name="thumbnailHeight">Height of thumbnail</param>
    /// <param name="targetDirectory">If null, will write to <see cref="DirectoryService.CoverImageDirectory"/></param>
    /// <returns>File name with extension of the file. </returns>
    string CreateThumbnailFromBase64(string encodedImage, string fileName, EncodeFormat encodeFormat, int thumbnailWidth = 320, int thumbnailHeight = 455, string? targetDirectory = null);

    /// <summary>
    /// Creates a thumbnail from an image file already on disk (e.g. one staged in the temp directory).
    /// </summary>
    /// <param name="sourceFile">Absolute path to the source image</param>
    /// <param name="fileName"></param>
    /// <param name="encodeFormat">Convert and save as encoding format</param>
    /// <param name="thumbnailWidth">Width of thumbnail</param>
    /// <param name="thumbnailHeight"></param>
    /// <param name="targetDirectory">If null, will write to <see cref="DirectoryService.CoverImageDirectory"/></param>
    /// <returns>File name with extension of the file.</returns>
    string CreateThumbnailFromFile(string sourceFile, string fileName, EncodeFormat encodeFormat, int thumbnailWidth = 320, int thumbnailHeight = 455, string? targetDirectory = null);
    /// <summary>
    /// Writes out a thumbnail by stream input
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="fileName"></param>
    /// <param name="outputDirectory"></param>
    /// <param name="encodeFormat"></param>
    /// <returns></returns>
    string WriteCoverThumbnail(Stream stream, string fileName, string outputDirectory, EncodeFormat encodeFormat, CoverImageSize size = CoverImageSize.Default);
    /// <summary>
    /// Writes out a thumbnail by file path input
    /// </summary>
    /// <param name="sourceFile"></param>
    /// <param name="fileName"></param>
    /// <param name="outputDirectory"></param>
    /// <param name="encodeFormat"></param>
    /// <returns></returns>
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
    /// Performs I/O to determine if the file is a valid Image
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<bool> IsImage(string filePath, CancellationToken ct = default);
    void UpdateColorScape(IHasCoverImage entity);

    /// <summary>
    /// Downloads an image from the given URL, resizes it, and saves it to the covers directory.
    /// <remarks>Caller is responsible for validating the URL before calling this method (via <see cref="IUrlValidationService"/> ).</remarks>
    /// </summary>
    /// <param name="url">The URL to download the image from</param>
    /// <param name="fileName">Filename without extension</param>
    /// <param name="encodeFormat">Convert and save as encoding format</param>
    /// <param name="thumbnailWidth">Width of thumbnail</param>
    /// <param name="thumbnailHeight">Height of thumbnail</param>
    /// <returns>File name with extension of the saved file, or empty string on failure</returns>
    Task<string> CreateThumbnailFromUrl(string url, string fileName, EncodeFormat encodeFormat, int thumbnailWidth = 320, int thumbnailHeight = 455);
}
