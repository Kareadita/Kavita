using System;
using System.IO;
using System.Linq;
using API.Comparators;
using API.Entities;
using API.Interfaces.Services;
using Microsoft.Extensions.Logging;
using NetVips;

namespace API.Services
{

  public class ImageService : IImageService
  {
    private readonly ILogger<ImageService> _logger;
    private readonly IDirectoryService _directoryService;
    private readonly NaturalSortComparer _naturalSortComparer;

    public ImageService(ILogger<ImageService> logger, IDirectoryService directoryService)
    {
      _logger = logger;
      _directoryService = directoryService;
      _naturalSortComparer = new NaturalSortComparer();
    }

    public string GetCoverFile(MangaFile file)
    {
      var directory = Path.GetDirectoryName(file.FilePath);
      if (string.IsNullOrEmpty(directory))
      {
        _logger.LogError("Could not find Directory for {File}", file.FilePath);
        return null;
      }

      var firstImage = _directoryService.GetFilesWithExtension(directory, Parser.Parser.ImageFileExtensions)
        .OrderBy(f => f, _naturalSortComparer).FirstOrDefault();

      return firstImage;
    }

    public byte[] GetCoverImage(string path, bool createThumbnail = false)
    {
      if (string.IsNullOrEmpty(path)) return Array.Empty<byte>();

      try
      {
        if (createThumbnail)
        {
          using var thumbnail = Image.Thumbnail(path, MetadataService.ThumbnailWidth);
          return thumbnail.WriteToBuffer(".jpg");
        }

        using var img = Image.NewFromFile(path);
        using var stream = new MemoryStream();
        img.JpegsaveStream(stream);
        return stream.ToArray();
      }
      catch (Exception ex)
      {
        _logger.LogWarning(ex, "[GetCoverImage] There was an error and prevented thumbnail generation on {ImageFile}. Defaulting to no cover image", path);
      }

      return Array.Empty<byte>();
    }
  }
}
