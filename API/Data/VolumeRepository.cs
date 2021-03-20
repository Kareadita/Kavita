using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Interfaces;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data
{
    public class VolumeRepository : IVolumeRepository
    {
        private readonly DataContext _context;
        private readonly IMapper _mapper;
        private readonly ILogger _logger;

        public VolumeRepository(DataContext context, IMapper mapper, ILogger logger)
        {
            _context = context;
            _mapper = mapper;
            _logger = logger;
        }
        
        public void Update(Volume volume)
        {
            _context.Entry(volume).State = EntityState.Modified;
        }

        /// <summary>
        /// Returns a Chapter for an Id. Includes linked <see cref="MangaFile"/>s.
        /// </summary>
        /// <param name="chapterId"></param>
        /// <returns></returns>
        public async Task<Chapter> GetChapterAsync(int chapterId)
        {
            return await _context.Chapter
                .Include(c => c.Files)
                .SingleOrDefaultAsync(c => c.Id == chapterId);
        }
        
        
        /// <summary>
        /// Returns Chapters for a volume id.
        /// </summary>
        /// <param name="volumeId"></param>
        /// <returns></returns>
        public async Task<IList<Chapter>> GetChaptersAsync(int volumeId)
        {
            return await _context.Chapter
                .Where(c => c.VolumeId == volumeId)
                .ToListAsync();
        }

        /// <summary>
        /// Returns the cover image for a chapter id.
        /// </summary>
        /// <param name="chapterId"></param>
        /// <returns></returns>
        public async Task<byte[]> GetChapterCoverImageAsync(int chapterId)
        {
            return await _context.Chapter
                .Where(c => c.Id == chapterId)
                .Select(c => c.CoverImage)
                .AsNoTracking()
                .SingleOrDefaultAsync();
        }


        public async Task<ChapterDto> GetChapterDtoAsync(int chapterId)
        {
            var chapter = await _context.Chapter
                .Include(c => c.Files)
                .ProjectTo<ChapterDto>(_mapper.ConfigurationProvider)
                .AsNoTracking()
                .SingleOrDefaultAsync(c => c.Id == chapterId);

            return chapter;
        }

        public async Task<IList<MangaFile>> GetFilesForChapter(int chapterId)
        {
            return await _context.MangaFile
                .Where(c => chapterId == c.Id)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Gets the first (ordered) volume/chapter in a series where the user has progress on it. Only completed volumes/chapters, next entity shouldn't
        /// have any read progress on it. 
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="libraryId"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        public async Task<IEnumerable<InProgressChapterDto>> GetContinueReading(int userId, int libraryId, int limit)
        {
            /** TODO: Fix this SQL
             * SELECT * FROM 
            (
                  SELECT * FROM Chapter C WHERE C.VolumeId IN (SELECT Id from Volume where SeriesId = 1912)
            ) C INNER JOIN AppUserProgresses AUP ON AUP.ChapterId = C.Id
                INNER JOIN Series S ON AUP.SeriesId = S.Id
    WHERE AUP.AppUserId = 1 AND AUP.PagesRead < C.Pages
             */
            _logger.LogInformation("Get Continue Reading");
            var volumeQuery = _context.Volume
                .Join(_context.AppUserProgresses, v => v.Id, aup => aup.VolumeId, (volume, progress) => new
                {
                    volume,
                    progress
                })
                .Where(arg => arg.volume.SeriesId == arg.progress.SeriesId && arg.progress.AppUserId == userId)
                .AsNoTracking()
                .Select(arg => new
                {
                    VolumeId = arg.volume.Id,
                    VolumeNumber = arg.volume.Number
                }); // I think doing a join on this would be better

            var volumeIds = (await volumeQuery.ToListAsync()).Select(s => s.VolumeId);
            
            var chapters2 = await _context.Chapter.Where(c => volumeIds.Contains(c.VolumeId))
                .Join(_context.AppUserProgresses, chapter => chapter.Id, aup => aup.ChapterId, (chapter, progress) =>
                    new
                    {
                        chapter,
                        progress
                    })
                .Join(_context.Series, arg => arg.progress.SeriesId, s => s.Id, (arg, series) => new
                {
                    Chapter = arg.chapter,
                    Progress = arg.progress,
                    Series = series
                })
                .Where(o => o.Progress.AppUserId == userId && o.Progress.PagesRead < o.Series.Pages)
                .Select(arg => new
                {
                    Chapter = arg.Chapter,
                    Progress = arg.Progress,
                    SeriesId = arg.Series.Id,
                    SeriesName = arg.Series.Name,
                    LibraryId = arg.Series.LibraryId,
                    TotalPages = arg.Series.Pages
                })
                .OrderByDescending(d => d.Progress.LastModified)
                .Take(limit)
                .ToListAsync();

            return chapters2
                .OrderBy(c => float.Parse(c.Chapter.Number), new ChapterSortComparer())
                .DistinctBy(p => p.SeriesId)
                .Select(arg => new InProgressChapterDto()
                {
                    Id = arg.Chapter.Id,
                    Number = arg.Chapter.Number,
                    Range = arg.Chapter.Range,
                    SeriesId = arg.Progress.SeriesId,
                    SeriesName = arg.SeriesName,
                    LibraryId = arg.LibraryId,
                    Pages = arg.Chapter.Pages,
                    VolumeId = arg.Chapter.VolumeId
                });
            
            
            
            // var chapters = await _context.Chapter
            //     .Join(_context.AppUserProgresses, c => c.Id, p => p.ChapterId,
            //         (chapter, progress) =>
            //             new
            //             {
            //                 Chapter = chapter,
            //                 Progress = progress
            //             })
            //     .Join(_context.Series, arg => arg.Progress.SeriesId, series => series.Id, (arg, series) => 
            //         new
            //         {
            //             arg.Chapter,
            //             arg.Progress,
            //             Series = series,
            //             VolumeIds = _context.Volume.Where(v => v.SeriesId == series.Id).Select(s => s.Id).ToList()
            //         })
            //     .AsNoTracking()
            //     .Where(arg => arg.Progress.AppUserId == userId 
            //                   && arg.Progress.PagesRead < arg.Chapter.Pages
            //                   && arg.VolumeIds.Contains(arg.Progress.VolumeId))
            //     .OrderByDescending(d => d.Progress.LastModified)
            //     .Take(limit)
            //     .ToListAsync();

            // return chapters
            //     .OrderBy(c => float.Parse(c.Chapter.Number), new ChapterSortComparer())
            //     .DistinctBy(p => p.Series.Id)
            //     .Select(arg => new InProgressChapterDto()
            //     {
            //         Id = arg.Chapter.Id,
            //         Number = arg.Chapter.Number,
            //         Range = arg.Chapter.Range,
            //         SeriesId = arg.Progress.SeriesId,
            //         SeriesName = arg.Series.Name,
            //         LibraryId = arg.Series.LibraryId,
            //         Pages = arg.Chapter.Pages,
            //     });
        }
    }
}