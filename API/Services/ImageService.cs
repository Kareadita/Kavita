using System;
using System.IO;
using API.Interfaces.Services;
using Microsoft.Extensions.Logging;
using NetVips;

namespace API.Services
{

  public class ImageService : IImageService
  {
    private readonly ILogger<ImageService> _logger;

    public ImageService(ILogger<ImageService> logger)
    {
      _logger = logger;
    }

    public byte[] GetCoverImage(string imageFile, bool createThumbnail = false)
    {
      try
      {
        if (createThumbnail)
        {
          using var thumbnail = Image.Thumbnail(imageFile, MetadataService.ThumbnailWidth);
          return thumbnail.WriteToBuffer(".jpg");
        }

        using var img = Image.NewFromFile(imageFile);
        using var stream = new MemoryStream();
        img.JpegsaveStream(stream);
        return stream.ToArray();
      }
      catch (Exception ex)
      {
        _logger.LogWarning(ex, "[GetCoverImage] There was an error and prevented thumbnail generation on {ImageFile}. Defaulting to no cover image", imageFile);
      }

      return Array.Empty<byte>();
    }
  }
}
