﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.ReadingLists;
using API.Entities;
using API.Helpers;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface IReadingListRepository
{
    Task<PagedList<ReadingListDto>> GetReadingListDtosForUserAsync(int userId, bool includePromoted, UserParams userParams);
    Task<ReadingList> GetReadingListByIdAsync(int readingListId);
    Task<IEnumerable<ReadingListItemDto>> GetReadingListItemDtosByIdAsync(int readingListId, int userId);
    Task<ReadingListDto> GetReadingListDtoByIdAsync(int readingListId, int userId);
    Task<IEnumerable<ReadingListItemDto>> AddReadingProgressModifiers(int userId, IList<ReadingListItemDto> items);
    Task<ReadingListDto> GetReadingListDtoByTitleAsync(string title);
    Task<IEnumerable<ReadingListItem>> GetReadingListItemsByIdAsync(int readingListId);
    void Remove(ReadingListItem item);
    void BulkRemove(IEnumerable<ReadingListItem> items);
    void Update(ReadingList list);
}

public class ReadingListRepository : IReadingListRepository
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;

    public ReadingListRepository(DataContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public void Update(ReadingList list)
    {
        _context.Entry(list).State = EntityState.Modified;
    }

    public void Remove(ReadingListItem item)
    {
        _context.ReadingListItem.Remove(item);
    }

    public void BulkRemove(IEnumerable<ReadingListItem> items)
    {
        _context.ReadingListItem.RemoveRange(items);
    }


    public async Task<PagedList<ReadingListDto>> GetReadingListDtosForUserAsync(int userId, bool includePromoted, UserParams userParams)
    {
        var query = _context.ReadingList
            .Where(l => l.AppUserId == userId || (includePromoted &&  l.Promoted ))
            .OrderBy(l => l.LastModified)
            .ProjectTo<ReadingListDto>(_mapper.ConfigurationProvider)
            .AsNoTracking();

        return await PagedList<ReadingListDto>.CreateAsync(query, userParams.PageNumber, userParams.PageSize);
    }

    public async Task<ReadingList> GetReadingListByIdAsync(int readingListId)
    {
        return await _context.ReadingList
            .Where(r => r.Id == readingListId)
            .Include(r => r.Items.OrderBy(item => item.Order))
            .SingleOrDefaultAsync();
    }

    public async Task<IEnumerable<ReadingListItemDto>> GetReadingListItemDtosByIdAsync(int readingListId, int userId)
    {
        var userLibraries = _context.Library
            .Include(l => l.AppUsers)
            .Where(library => library.AppUsers.Any(user => user.Id == userId))
            .AsNoTracking()
            .Select(library => library.Id)
            .ToList();

        var items = await _context.ReadingListItem
            .Where(s => s.ReadingListId == readingListId)
            .Join(_context.Chapter, s => s.ChapterId, chapter => chapter.Id, (data, chapter) => new
            {
                TotalPages = chapter.Pages,
                ChapterNumber = chapter.Range,
                readingListItem = data
            })
            .Join(_context.Volume, s => s.readingListItem.VolumeId, volume => volume.Id, (data, volume) => new
            {
                data.readingListItem,
                data.TotalPages,
                data.ChapterNumber,
                VolumeId = volume.Id,
                VolumeNumber = volume.Name,
            })
            .Join(_context.Series, s => s.readingListItem.SeriesId, series => series.Id,
                (data, s) => new
                {
                    SeriesName = s.Name,
                    SeriesFormat = s.Format,
                    s.LibraryId,
                    data.readingListItem,
                    data.TotalPages,
                    data.ChapterNumber,
                    data.VolumeNumber,
                    data.VolumeId
                })
            .Select(data => new ReadingListItemDto()
            {
                Id = data.readingListItem.Id,
                ChapterId = data.readingListItem.ChapterId,
                Order = data.readingListItem.Order,
                SeriesId = data.readingListItem.SeriesId,
                SeriesName = data.SeriesName,
                SeriesFormat = data.SeriesFormat,
                PagesTotal = data.TotalPages,
                ChapterNumber = data.ChapterNumber,
                VolumeNumber = data.VolumeNumber,
                LibraryId = data.LibraryId,
                VolumeId = data.VolumeId,
                ReadingListId = data.readingListItem.ReadingListId
            })
            .Where(o => userLibraries.Contains(o.LibraryId))
            .OrderBy(rli => rli.Order)
            .AsNoTracking()
            .ToListAsync();

        // Attach progress information
        var fetchedChapterIds = items.Select(i => i.ChapterId);
        var progresses = await _context.AppUserProgresses
            .Where(p => fetchedChapterIds.Contains(p.ChapterId))
            .AsNoTracking()
            .ToListAsync();

        foreach (var progress in progresses)
        {
            var progressItem = items.SingleOrDefault(i => i.ChapterId == progress.ChapterId && i.ReadingListId == readingListId);
            if (progressItem == null) continue;

            progressItem.PagesRead = progress.PagesRead;
        }

        return items;
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

    public async Task<ReadingListDto> GetReadingListDtoByTitleAsync(string title)
    {
        return await _context.ReadingList
            .Where(r => r.Title.Equals(title))
            .ProjectTo<ReadingListDto>(_mapper.ConfigurationProvider)
            .SingleOrDefaultAsync();
    }

    public async Task<IEnumerable<ReadingListItem>> GetReadingListItemsByIdAsync(int readingListId)
    {
        return await _context.ReadingListItem
            .Where(r => r.ReadingListId == readingListId)
            .OrderBy(r => r.Order)
            .ToListAsync();
    }


}
