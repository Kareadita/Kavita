using API.Entities;

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
    /// <returns></returns>
    public string CreateThumbnail(string path, string fileName);
    /// <summary>
    /// Creates a Thumbnail version of a base64 image
    /// </summary>
    /// <param name="encodedImage">base64 encoded image</param>
    /// <returns></returns>
    public byte[] CreateThumbnailFromBase64(string encodedImage);
  }
}
