using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.ReadingLists;
using API.Entities;
using API.Interfaces.Repositories;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories
{
    public class ReadingListRepository : IReadingListRepository
    {
        private readonly DataContext _context;
        private readonly IMapper _mapper;

        public ReadingListRepository(DataContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }


        public async Task<IEnumerable<ReadingListDto>> GetReadingListDtosForUserAsync(int userId, bool includePromoted)
        {
            return await _context.ReadingList
                .Where(l => l.AppUserId == userId || (includePromoted &&  l.Promoted ))
                .OrderBy(l => l.LastModified)
                .ProjectTo<ReadingListDto>(_mapper.ConfigurationProvider)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<ReadingList> GetReadingListByIdAsync(int readingListId)
        {
            return await _context.ReadingList
                .Where(r => r.Id == readingListId)
                .Include(r => r.Items)
                .SingleOrDefaultAsync();
        }

        public async Task<IEnumerable<ReadingListItemDto>> GetReadingListItemDtosByIdAsync(int readingListId, int userId)
        {

            var seriesIds = await _context.ReadingListItem
                .Where(rli => rli.ReadingListId == readingListId)
                .Select(rli => rli.SeriesId)
                .ToListAsync();

            var items = _context.Series
                .Where(s => seriesIds.Contains(s.Id))
                .Join(_context.ReadingListItem, s => s.Id, readingListItem => readingListItem.SeriesId, (s, readingListItem) => new
                {
                    SeriesName = s.Name,
                    SeriesFormat = s.Format,
                    readingListItem
                })
                .Join(_context.Chapter, s => s.readingListItem.ChapterId, chapter => chapter.Id, (data, chapter) => new
                {
                    SeriesName = data.SeriesName,
                    SeriesFormat = data.SeriesFormat,
                    readingListItem = data.readingListItem,
                    TotalPages = chapter.Pages,
                    ChapterNumber = chapter.Range,
                })
                .Join(_context.Volume, s => s.readingListItem.VolumeId, volume =>  volume.Id, (data, volume) => new
                {
                    SeriesName = data.SeriesName,
                    SeriesFormat = data.SeriesFormat,
                    readingListItem = data.readingListItem,
                    TotalPages = data.TotalPages,
                    ChapterNumber = data.ChapterNumber,
                    VolumeNumber = volume.Name,
                })
                .Select(data => new ReadingListItemDto()
                {
                    ChapterId = data.readingListItem.ChapterId,
                    Order = data.readingListItem.Order,
                    SeriesId = data.readingListItem.SeriesId,
                    SeriesName = data.SeriesName,
                    SeriesFormat = data.SeriesFormat,
                    PagesTotal = data.TotalPages,
                    ChapterNumber = data.ChapterNumber,
                    VolumeNumber = data.VolumeNumber
                })
                .OrderBy(rli => rli.Order)
                .AsNoTracking();

            // var items2 = _context.ReadingListItem
            //     .Where(s => seriesIds.Contains(s.Id))
            //     .Include(r => r.Series)
            //     .Include(r => r.Volume)
            //     .Include(r => r.Chapter)
            //     .Select
            //     .Join(_context.ReadingListItem, s => s.Id, readingListItem => readingListItem.SeriesId, (s, readingListItem) => new
            //     {
            //         SeriesName = s.Name,
            //         SeriesFormat = s.Format,
            //         readingListItem
            //     })
            //     .Join(_context.Chapter, s => s.readingListItem.ChapterId, chapter => chapter.Id, (data, chapter) => new
            //     {
            //         SeriesName = data.SeriesName,
            //         SeriesFormat = data.SeriesFormat,
            //         readingListItem = data.readingListItem,
            //         TotalPages = chapter.Pages,
            //         ChapterNumber = chapter.Range,
            //     })
            //     .Join(_context.Volume, s => s.readingListItem.VolumeId, volume =>  volume.Id, (data, volume) => new
            //     {
            //         SeriesName = data.SeriesName,
            //         SeriesFormat = data.SeriesFormat,
            //         readingListItem = data.readingListItem,
            //         TotalPages = data.TotalPages,
            //         ChapterNumber = data.ChapterNumber,
            //         VolumeNumber = volume.Name,
            //     })
            //     .Select(data => new ReadingListItemDto()
            //     {
            //         ChapterId = data.readingListItem.ChapterId,
            //         Order = data.readingListItem.Order,
            //         SeriesId = data.readingListItem.SeriesId,
            //         SeriesName = data.SeriesName,
            //         SeriesFormat = data.SeriesFormat,
            //         PagesTotal = data.TotalPages,
            //         ChapterNumber = data.ChapterNumber,
            //         VolumeNumber = data.VolumeNumber
            //     })
            //     .OrderBy(rli => rli.Order)
            //     .AsNoTracking();


            return await items.ToListAsync();
        }

        public async Task<ReadingListDto> GetReadingListDtoByIdAsync(int readingListId, int userId)
        {
            return await _context.ReadingList
                .Where(r => r.Id == readingListId && (r.AppUserId == userId || r.Promoted))
                .ProjectTo<ReadingListDto>(_mapper.ConfigurationProvider)
                .SingleOrDefaultAsync();
        }

        public async Task<IEnumerable<ReadingListItemDto>> AddReadingProgressModifiers(int userId, IList<ReadingListItemDto> items)
        {
            var chapterIds = items.Select(i => i.ChapterId).Distinct().ToList();
            var userProgress = await _context.AppUserProgresses
                .Where(p => p.AppUserId == userId && chapterIds.Contains(p.ChapterId))
                .AsNoTracking()
                .ToListAsync();

            foreach (var item in items)
            {
                var progress = userProgress.Where(p => p.ChapterId == item.ChapterId);
                item.PagesRead = progress.Sum(p => p.PagesRead);
            }

            return items;
        }


    }
}
