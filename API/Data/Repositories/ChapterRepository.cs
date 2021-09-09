using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.Reader;
using API.Entities;
using API.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories
{
    public class ChapterRepository : IChapterRepository
    {
        private readonly DataContext _context;

        public ChapterRepository(DataContext context)
        {
            _context = context;
        }

        public void Update(Chapter chapter)
        {
            _context.Entry(chapter).State = EntityState.Modified;
        }

        public async Task<IEnumerable<Chapter>> GetChaptersByIdsAsync(IList<int> chapterIds)
        {
            return await _context.Chapter
                .Where(c => chapterIds.Contains(c.Id))
                .Include(c => c.Volume)
                .ToListAsync();
        }

        // TODO: Move over Chapter based queries here

        /// <summary>
        /// Populates a partial IChapterInfoDto
        /// </summary>
        /// <returns></returns>
        public async Task<IChapterInfoDto> GetChapterInfoDtoAsync(int chapterId)
        {
            return await _context.Chapter
                .Where(c => c.Id == chapterId)
                .Join(_context.Volume, c => c.VolumeId, v => v.Id, (chapter, volume) => new
                {
                    ChapterNumber = chapter.Range,
                    VolumeNumber = volume.Number,
                    VolumeId = volume.Id,
                    chapter.IsSpecial,
                    volume.SeriesId,
                    chapter.Pages
                })
                .Join(_context.Series, data => data.SeriesId, series => series.Id, (data, series) => new
                {
                    data.ChapterNumber,
                    data.VolumeNumber,
                    data.VolumeId,
                    data.IsSpecial,
                    data.SeriesId,
                    data.Pages,
                    SeriesFormat = series.Format,
                    SeriesName = series.Name,
                    series.LibraryId
                })
                .Select(data => new BookInfoDto()
                {
                    ChapterNumber = data.ChapterNumber,
                    VolumeNumber = data.VolumeNumber + string.Empty,
                    VolumeId = data.VolumeId,
                    IsSpecial = data.IsSpecial,
                    SeriesId =data.SeriesId,
                    SeriesFormat = data.SeriesFormat,
                    SeriesName = data.SeriesName,
                    LibraryId = data.LibraryId,
                    Pages = data.Pages
                })
                .AsNoTracking()
                .SingleAsync();
        }
    }
}
