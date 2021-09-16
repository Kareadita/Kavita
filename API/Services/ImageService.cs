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

    public ImageService(ILogger<ImageService> logger, IDirectoryService directoryService)
    {
      _logger = logger;
      _directoryService = directoryService;
    }

    /// <summary>
    /// Finds the first image in the directory of the first file. Does not check for "cover/folder".ext files to override.
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    public string GetCoverFile(MangaFile file)
    {
      var directory = Path.GetDirectoryName(file.FilePath);
      if (string.IsNullOrEmpty(directory))
      {
        _logger.LogError("Could not find Directory for {File}", file.FilePath);
        return null;
      }

      var firstImage = _directoryService.GetFilesWithExtension(directory, Parser.Parser.ImageFileExtensions)
        .OrderBy(f => f, new NaturalSortComparer()).FirstOrDefault();

      return firstImage;
    }

    public string GetCoverImage(string path, string fileName)
    {
      if (string.IsNullOrEmpty(path)) return String.Empty;

      try
      {
          return CreateThumbnail(path,  fileName);
      }
      catch (Exception ex)
      {
        _logger.LogWarning(ex, "[GetCoverImage] There was an error and prevented thumbnail generation on {ImageFile}. Defaulting to no cover image", path);
      }

      return String.Empty;
    }

    /// <summary>
    /// Writes the bytes[] to the full path. Will overwrite if already exists
    /// </summary>
    /// <param name="image"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    public void WriteBytesToDisk(byte[] image, string path)
    {
        File.WriteAllBytesAsync(path, image);
    }


    /// <inheritdoc />
    public string CreateThumbnail(string path, string fileName)
    {
        try
        {
            using var thumbnail = Image.Thumbnail(path, MetadataService.ThumbnailWidth);
            var filename = Path.Join(DirectoryService.CoverImageDirectory, fileName + ".png");
            thumbnail.WriteToFile(filename);
            return filename;
            //return thumbnail.WriteToBuffer(".jpg");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error creating thumbnail from url");
        }

        return String.Empty;
    }


    /// <inheritdoc />
    public byte[] CreateThumbnailFromBase64(string encodedImage)
    {
        try
        {
            using var thumbnail = Image.ThumbnailBuffer(Convert.FromBase64String(encodedImage), MetadataService.ThumbnailWidth);
            return thumbnail.WriteToBuffer(".jpg");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error creating thumbnail from url");
        }

        return Array.Empty<byte>();
    }
  }
}
