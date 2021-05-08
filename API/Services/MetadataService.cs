using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
using API.Entities;
using API.Entities.Enums;
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
       private readonly IBookService _bookService;
       private readonly ChapterSortComparer _chapterSortComparer = new ChapterSortComparer();

       public MetadataService(IUnitOfWork unitOfWork, ILogger<MetadataService> logger, IArchiveService archiveService, IBookService bookService)
       {
          _unitOfWork = unitOfWork;
          _logger = logger;
          _archiveService = archiveService;
          _bookService = bookService;
       }
       
       private static bool ShouldFindCoverImage(byte[] coverImage, bool forceUpdate = false)
       {
          return forceUpdate || coverImage == null || !coverImage.Any();
       }

       private byte[] GetCoverImage(MangaFile file, bool createThumbnail = true)
       {
          if (file.Format == MangaFormat.Book)
          {
             return _bookService.GetCoverImage(file.FilePath, createThumbnail);
          }
          else
          {
             return _archiveService.GetCoverImage(file.FilePath, createThumbnail);   
          }
       }

       public void UpdateMetadata(Chapter chapter, bool forceUpdate)
       {
          var firstFile = chapter.Files.OrderBy(x => x.Chapter).FirstOrDefault();
          if (ShouldFindCoverImage(chapter.CoverImage, forceUpdate) && firstFile != null && !new FileInfo(firstFile.FilePath).IsLastWriteLessThan(firstFile.LastModified))
          {
             chapter.Files ??= new List<MangaFile>();
             chapter.CoverImage = GetCoverImage(firstFile); 
          }
       }


       public void UpdateMetadata(Volume volume, bool forceUpdate)
       {
          if (volume != null && ShouldFindCoverImage(volume.CoverImage, forceUpdate))
          {
             volume.Chapters ??= new List<Chapter>();
             var firstChapter = volume.Chapters.OrderBy(x => double.Parse(x.Number), _chapterSortComparer).FirstOrDefault();

             // Skip calculating Cover Image (I/O) if the chapter already has it set
             if (firstChapter == null || ShouldFindCoverImage(firstChapter.CoverImage))
             {
                var firstFile = firstChapter?.Files.OrderBy(x => x.Chapter).FirstOrDefault();
                if (firstFile != null && !new FileInfo(firstFile.FilePath).IsLastWriteLessThan(firstFile.LastModified))
                {
                   volume.CoverImage = GetCoverImage(firstFile);
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
          if (series == null) return;
          if (ShouldFindCoverImage(series.CoverImage, forceUpdate))
          {
             series.Volumes ??= new List<Volume>();
             var firstCover = series.Volumes.GetCoverImage(series.Library.Type);
             byte[] coverImage = null; 
             if (firstCover == null && series.Volumes.Any())
             {
                // If firstCover is null and one volume, the whole series is Chapters under Vol 0. 
                if (series.Volumes.Count == 1)
                {
                   coverImage = series.Volumes[0].Chapters.OrderBy(c => double.Parse(c.Number), _chapterSortComparer)
                      .FirstOrDefault(c => !c.IsSpecial)?.CoverImage;
                }

                if (coverImage == null)
                {
                   coverImage = series.Volumes[0].Chapters.OrderBy(c => double.Parse(c.Number), _chapterSortComparer)
                      .FirstOrDefault()?.CoverImage;
                }
             }
             series.CoverImage = firstCover?.CoverImage ?? coverImage;
          }

          UpdateSeriesSummary(series, forceUpdate);
       }

       private void UpdateSeriesSummary(Series series, bool forceUpdate)
       {
          if (!string.IsNullOrEmpty(series.Summary) && !forceUpdate) return;
          
          var isBook = series.Library.Type == LibraryType.Book;
          var firstVolume = series.Volumes.FirstWithChapters(isBook);
          var firstChapter = firstVolume?.Chapters.GetFirstChapterWithFiles();

          // NOTE: This suffers from code changes not taking effect due to stale data
          var firstFile = firstChapter?.Files.FirstOrDefault();
          if (firstFile != null &&
              (forceUpdate || firstFile.HasFileBeenModified())) // !new FileInfo(firstFile.FilePath).IsLastWriteLessThan(firstFile.LastModified)
          {
             var summary = isBook ? _bookService.GetSummaryInfo(firstFile.FilePath) : _archiveService.GetSummaryInfo(firstFile.FilePath);
             if (string.IsNullOrEmpty(series.Summary))
             {
                series.Summary = summary;
             }

             firstFile.LastModified = DateTime.Now;
          }
       }


       public void RefreshMetadata(int libraryId, bool forceUpdate = false)
       {
          var sw = Stopwatch.StartNew();
          var library = Task.Run(() => _unitOfWork.LibraryRepository.GetFullLibraryForIdAsync(libraryId)).GetAwaiter().GetResult();

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
       
       
       public void RefreshMetadataForSeries(int libraryId, int seriesId)
       {
          var sw = Stopwatch.StartNew();
          var library = Task.Run(() => _unitOfWork.LibraryRepository.GetFullLibraryForIdAsync(libraryId)).GetAwaiter().GetResult();
          
          var series = library.Series.SingleOrDefault(s => s.Id == seriesId);
          if (series == null)
          {
             _logger.LogError("Series {SeriesId} was not found on Library {LibraryName}", seriesId, libraryId);
             return;
          }
          _logger.LogInformation("Beginning metadata refresh of {SeriesName}", series.Name);
          foreach (var volume in series.Volumes)
          {
             foreach (var chapter in volume.Chapters)
             {
                UpdateMetadata(chapter, true);
             }
                
             UpdateMetadata(volume, true);
          }

          UpdateMetadata(series, true);
          _unitOfWork.SeriesRepository.Update(series);


          if (_unitOfWork.HasChanges() && Task.Run(() => _unitOfWork.Complete()).Result)
          {
             _logger.LogInformation("Updated metadata for {SeriesName} in {ElapsedMilliseconds} milliseconds", series.Name, sw.ElapsedMilliseconds);
          }
       }
    }
}