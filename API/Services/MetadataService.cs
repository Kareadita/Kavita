using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using API.Extensions;
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
          var firstFile = chapter.Files.OrderBy(x => x.Chapter).FirstOrDefault();
          if (ShouldFindCoverImage(chapter.CoverImage, forceUpdate) && firstFile != null && !new FileInfo(firstFile.FilePath).IsLastWriteLessThan(firstFile.LastModified))
          {
             chapter.Files ??= new List<MangaFile>();
             chapter.CoverImage = _archiveService.GetCoverImage(firstFile.FilePath, true);
          }
       }


       public void UpdateMetadata(Volume volume, bool forceUpdate)
       {
          if (volume != null && ShouldFindCoverImage(volume.CoverImage, forceUpdate))
          {
             // TODO: Replace this with ChapterSortComparator
             volume.Chapters ??= new List<Chapter>();
             var firstChapter = volume.Chapters.OrderBy(x => double.Parse(x.Number)).FirstOrDefault(); 
             
             // Skip calculating Cover Image (I/O) if the chapter already has it set
             if (firstChapter == null || ShouldFindCoverImage(firstChapter.CoverImage))
             {
                var firstFile = firstChapter?.Files.OrderBy(x => x.Chapter).FirstOrDefault();
                if (firstFile != null && !new FileInfo(firstFile.FilePath).IsLastWriteLessThan(firstFile.LastModified))
                {
                   volume.CoverImage = _archiveService.GetCoverImage(firstFile.FilePath, true);
                }
             }
             else
             {
                volume.CoverImage = firstChapter.CoverImage;
             }
          }
       }

       public void UpdateMetadata(Series series, bool forceUpdate)
       {
          // TODO: Use new ChapterSortComparer() here instead
          if (series == null) return;
          if (ShouldFindCoverImage(series.CoverImage, forceUpdate))
          {
             series.Volumes ??= new List<Volume>();
             var firstCover = series.Volumes.OrderBy(x => x.Number).FirstOrDefault(x => x.Number != 0);
             byte[] coverImage = null; 
             if (firstCover == null && series.Volumes.Any())
             {
                // If firstCover is null and one volume, the whole series is Chapters under Vol 0. 
                if (series.Volumes.Count == 1)
                {
                   coverImage = series.Volumes[0].Chapters.OrderBy(c => double.Parse(c.Number))
                      .FirstOrDefault(c => !c.IsSpecial)?.CoverImage;
                }

                if (coverImage == null)
                {
                   coverImage = series.Volumes[0].Chapters.OrderBy(c => double.Parse(c.Number))
                      .FirstOrDefault()?.CoverImage;
                }
             }
             series.CoverImage = firstCover?.CoverImage ?? coverImage;
          }

          if (!string.IsNullOrEmpty(series.Summary) && !forceUpdate) return;
          
          var firstVolume = series.Volumes.FirstOrDefault(v => v.Chapters.Any() && v.Number == 1);
          var firstChapter = firstVolume?.Chapters.FirstOrDefault(c => c.Files.Any());
          
          var firstFile = firstChapter?.Files.FirstOrDefault();
          if (firstFile != null && !new FileInfo(firstFile.FilePath).DoesLastWriteMatch(firstFile.LastModified))
          {
             series.Summary = _archiveService.GetSummaryInfo(firstFile.FilePath);
             firstFile.LastModified = DateTime.Now;
          }
       }
       
       
       public void RefreshMetadata(int libraryId, bool forceUpdate = false)
       {
          var sw = Stopwatch.StartNew();
          var library = Task.Run(() => _unitOfWork.LibraryRepository.GetFullLibraryForIdAsync(libraryId)).Result;

          // TODO: See if we can break this up into multiple threads that process 20 series at a time then save so we can reduce amount of memory used
          _logger.LogInformation("Beginning metadata refresh of {LibraryName}", library.Name);
          foreach (var series in library.Series)
          {
             foreach (var volume in series.Volumes)
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