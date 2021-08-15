using API.Entities;

namespace API.Interfaces.Services
{
  public interface IImageService
  {
    byte[] GetCoverImage(string path, bool createThumbnail = false);
    string GetCoverFile(MangaFile file);
    /// <summary>
    /// Creates a Thumbnail version of an image
    /// </summary>
    /// <param name="path">Path to the image file</param>
    /// <returns></returns>
    public byte[] CreateThumbnail(string path);
    /// <summary>
    /// Creates a Thumbnail version of a base64 image
    /// </summary>
    /// <param name="encodedImage">base64 encoded image</param>
    /// <returns></returns>
    public byte[] CreateThumbnailFromBase64(string encodedImage);
  }
}
