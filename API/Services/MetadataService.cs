using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using API.Interfaces;
using API.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace API.Services
{
    public class MetadataService : IMetadataService
    {
       private readonly IUnitOfWork _unitOfWork;
       private readonly ILogger<MetadataService> _logger;
       private readonly IArchiveService _archiveService;

       public MetadataService(IUnitOfWork unitOfWork, ILogger<MetadataService> logger, IArchiveService archiveService)
       {
          _unitOfWork = unitOfWork;
          _logger = logger;
          _archiveService = archiveService;
       }
       
       private static bool ShouldFindCoverImage(byte[] coverImage, bool forceUpdate = false)
       {
          return forceUpdate || coverImage == null || !coverImage.Any();
       }

       public void UpdateMetadata(Chapter chapter, bool forceUpdate)
       {
          if (chapter != null && ShouldFindCoverImage(chapter.CoverImage, forceUpdate))
          {
             chapter.Files ??= new List<MangaFile>();
             var firstFile = chapter.Files.OrderBy(x => x.Chapter).FirstOrDefault();
             if (firstFile != null) chapter.CoverImage = _archiveService.GetCoverImage(firstFile.FilePath, true);
          }
       }

       public void UpdateMetadata(Volume volume, bool forceUpdate)
       {
          if (volume != null && ShouldFindCoverImage(volume.CoverImage, forceUpdate))
          {
             // TODO: Create a custom sorter for Chapters so it's consistent across the application
             volume.Chapters ??= new List<Chapter>();
             var firstChapter = volume.Chapters.OrderBy(x => Double.Parse(x.Number)).FirstOrDefault();
             var firstFile = firstChapter?.Files.OrderBy(x => x.Chapter).FirstOrDefault();
             if (firstFile != null) volume.CoverImage = _archiveService.GetCoverImage(firstFile.FilePath, true);
          }
       }

       public void UpdateMetadata(Series series, bool forceUpdate)
       {
          // TODO: this doesn't actually invoke finding a new cover. Also all these should be groupped ideally so we limit
          // disk I/O to one method.
          if (series == null) return;
          if (ShouldFindCoverImage(series.CoverImage, forceUpdate))
          {
             series.Volumes ??= new List<Volume>();
             var firstCover = series.Volumes.OrderBy(x => x.Number).FirstOrDefault(x => x.Number != 0);
             if (firstCover == null && series.Volumes.Any())
             {
                firstCover = series.Volumes.FirstOrDefault(x => x.Number == 0);
             }
             series.CoverImage = firstCover?.CoverImage;
          }
          
          if (string.IsNullOrEmpty(series.Summary) || forceUpdate)
          {
             series.Summary = "";
          }
       }
       
       public void RefreshMetadata(int libraryId, bool forceUpdate = false)
       {
          var sw = Stopwatch.StartNew();
          var library = Task.Run(() => _unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId)).Result;
          var allSeries = Task.Run(() => _unitOfWork.SeriesRepository.GetSeriesForLibraryIdAsync(libraryId)).Result.ToList();
          
          _logger.LogInformation("Beginning metadata refresh of {LibraryName}", library.Name);
          foreach (var series in allSeries)
          {
             series.NormalizedName = Parser.Parser.Normalize(series.Name);
             
             var volumes = Task.Run(() => _unitOfWork.SeriesRepository.GetVolumes(series.Id)).Result.ToList();
             foreach (var volume in volumes)
             {
                foreach (var chapter in volume.Chapters)
                {
                   UpdateMetadata(chapter, forceUpdate);
                }
                
                UpdateMetadata(volume, forceUpdate);
             }

             UpdateMetadata(series, forceUpdate);
             _unitOfWork.SeriesRepository.Update(series);
          }


          if (_unitOfWork.HasChanges() && Task.Run(() => _unitOfWork.Complete()).Result)
          {
             _logger.LogInformation("Updated metadata for {LibraryName} in {ElapsedMilliseconds} milliseconds", library.Name, sw.ElapsedMilliseconds);
          }
       }
    }
}