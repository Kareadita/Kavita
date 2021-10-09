using API.Entities;
using API.Services;

namespace API.Interfaces.Services
{
  public interface IImageService
  {
    string GetCoverImage(string path, string fileName);
    string GetCoverFile(MangaFile file);
    /// <summary>
    /// Creates a Thumbnail version of an image
    /// </summary>
    /// <param name="path">Path to the image file</param>
    /// <returns>File name with extension of the file. This will always write to <see cref="DirectoryService.CoverImageDirectory"/></returns>
    public string CreateThumbnail(string path, string fileName);
    /// <summary>
    /// Creates a Thumbnail version of a base64 image
    /// </summary>
    /// <param name="encodedImage">base64 encoded image</param>
    /// <returns>File name with extension of the file. This will always write to <see cref="DirectoryService.CoverImageDirectory"/></returns>
    public string CreateThumbnailFromBase64(string encodedImage, string fileName);
  }
}
