namespace API.Interfaces.Services
{
  public interface IImageService
  {
    byte[] GetCoverImage(string path, bool createThumbnail = false);
  }
}
