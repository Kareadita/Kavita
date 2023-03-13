﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.DTOs.ReadingLists;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Extensions.QueryExtensions;
using API.Helpers;
using API.Services;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

[Flags]
public enum ReadingListIncludes
{
    None = 1,
    Items = 2,
    ItemChapter = 4,
}

public interface IReadingListRepository
{
    Task<PagedList<ReadingListDto>> GetReadingListDtosForUserAsync(int userId, bool includePromoted, UserParams userParams);
    Task<ReadingList?> GetReadingListByIdAsync(int readingListId, ReadingListIncludes includes = ReadingListIncludes.None);
    Task<IEnumerable<ReadingListItemDto>> GetReadingListItemDtosByIdAsync(int readingListId, int userId);
    Task<ReadingListDto?> GetReadingListDtoByIdAsync(int readingListId, int userId);
    Task<IEnumerable<ReadingListItemDto>> AddReadingProgressModifiers(int userId, IList<ReadingListItemDto> items);
    Task<ReadingListDto?> GetReadingListDtoByTitleAsync(int userId, string title);
    Task<IEnumerable<ReadingListItem>> GetReadingListItemsByIdAsync(int readingListId);
    Task<IEnumerable<ReadingListDto>> GetReadingListDtosForSeriesAndUserAsync(int userId, int seriesId,
        bool includePromoted);
    void Remove(ReadingListItem item);
    void Add(ReadingList list);
    void BulkRemove(IEnumerable<ReadingListItem> items);
    void Update(ReadingList list);
    Task<int> Count();
    Task<string?> GetCoverImageAsync(int readingListId);
    Task<IList<string>> GetAllCoverImagesAsync();
    Task<bool> ReadingListExists(string name);
    IEnumerable<PersonDto> GetReadingListCharactersAsync(int readingListId);
    Task<IList<ReadingList>> GetAllWithNonWebPCovers();
    Task<IList<string>> GetFirstFourCoverImagesByReadingListId(int readingListId);
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

    public void Add(ReadingList list)
    {
        _context.Add(list);
    }

    public async Task<int> Count()
    {
        return await _context.ReadingList.CountAsync();
    }

    public async Task<string?> GetCoverImageAsync(int readingListId)
    {
        return await _context.ReadingList
            .Where(c => c.Id == readingListId)
            .Select(c => c.CoverImage)
            .SingleOrDefaultAsync();
    }

    public async Task<IList<string>> GetAllCoverImagesAsync()
    {
        return (await _context.ReadingList
            .Select(t => t.CoverImage)
            .Where(t => !string.IsNullOrEmpty(t))
            .ToListAsync())!;
    }

    public async Task<bool> ReadingListExists(string name)
    {
        var normalized = name.ToNormalized();
        return await _context.ReadingList
            .AnyAsync(x => x.NormalizedTitle != null && x.NormalizedTitle.Equals(normalized));
    }

    public IEnumerable<PersonDto> GetReadingListCharactersAsync(int readingListId)
    {
        return _context.ReadingListItem
            .Where(item => item.ReadingListId == readingListId)
            .SelectMany(item => item.Chapter.People.Where(p => p.Role == PersonRole.Character))
            .OrderBy(p => p.NormalizedName)
            .ProjectTo<PersonDto>(_mapper.ConfigurationProvider)
            .AsEnumerable();
    }

    public async Task<IList<ReadingList>> GetAllWithNonWebPCovers()
    {
        return await _context.ReadingList
            .Where(c => !string.IsNullOrEmpty(c.CoverImage) && !c.CoverImage.EndsWith(".webp"))
            .ToListAsync();
    }

    /// <summary>
    /// If less than 4 images exist, will return nothing back. Will not be full paths, but just cover image filenames
    /// </summary>
    /// <param name="readingListId"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public async Task<IList<string>> GetFirstFourCoverImagesByReadingListId(int readingListId)
    {
        return await _context.ReadingListItem
            .Where(ri => ri.ReadingListId == readingListId)
            .Include(ri => ri.Chapter)
            .Where(ri => ri.Chapter.CoverImage != null)
            .Select(ri => ri.Chapter.CoverImage)
            .Take(4)
            .ToListAsync();
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
        var userAgeRating = (await _context.AppUser.SingleAsync(u => u.Id == userId)).AgeRestriction;
        var query = _context.ReadingList
            .Where(l => l.AppUserId == userId || (includePromoted &&  l.Promoted ))
            .Where(l => l.AgeRating >= userAgeRating)
            .OrderBy(l => l.LastModified)
            .ProjectTo<ReadingListDto>(_mapper.ConfigurationProvider)
            .AsNoTracking();

        return await PagedList<ReadingListDto>.CreateAsync(query, userParams.PageNumber, userParams.PageSize);
    }

    public async Task<IEnumerable<ReadingListDto>> GetReadingListDtosForSeriesAndUserAsync(int userId, int seriesId, bool includePromoted)
    {
        var query = _context.ReadingList
            .Where(l => l.AppUserId == userId || (includePromoted && l.Promoted ))
            .Where(l => l.Items.Any(i => i.SeriesId == seriesId))
            .AsSplitQuery()
            .OrderBy(l => l.Title)
            .ProjectTo<ReadingListDto>(_mapper.ConfigurationProvider)
            .AsNoTracking();

        return await query.ToListAsync();
    }

    public async Task<ReadingList?> GetReadingListByIdAsync(int readingListId, ReadingListIncludes includes = ReadingListIncludes.None)
    {
        return await _context.ReadingList
            .Where(r => r.Id == readingListId)
            .Includes(includes)
            .Include(r => r.Items.OrderBy(item => item.Order))
            .AsSplitQuery()
            .SingleOrDefaultAsync();
    }

    public async Task<IEnumerable<ReadingListItemDto>> GetReadingListItemDtosByIdAsync(int readingListId, int userId)
    {
        var userLibraries = _context.Library
            .Include(l => l.AppUsers)
            .Where(library => library.AppUsers.Any(user => user.Id == userId))
            .AsSplitQuery()
            .AsNoTracking()
            .Select(library => library.Id)
            .ToList();

        var items = await _context.ReadingListItem
            .Where(s => s.ReadingListId == readingListId)
            .Join(_context.Chapter, s => s.ChapterId, chapter => chapter.Id, (data, chapter) => new
            {
                TotalPages = chapter.Pages,
                ChapterNumber = chapter.Range,
                chapter.ReleaseDate,
                ReadingListItem = data,
                ChapterTitleName = chapter.TitleName,
                FileSize = chapter.Files.Sum(f => f.Bytes)

            })
            .Join(_context.Volume, s => s.ReadingListItem.VolumeId, volume => volume.Id, (data, volume) => new
            {
                data.ReadingListItem,
                data.TotalPages,
                data.ChapterNumber,
                data.ReleaseDate,
                data.ChapterTitleName,
                data.FileSize,
                VolumeId = volume.Id,
                VolumeNumber = volume.Name,
            })
            .Join(_context.Series, s => s.ReadingListItem.SeriesId, series => series.Id,
                (data, s) => new
                {
                    SeriesName = s.Name,
                    SeriesFormat = s.Format,
                    s.LibraryId,
                    data.ReadingListItem,
                    data.TotalPages,
                    data.ChapterNumber,
                    data.VolumeNumber,
                    data.VolumeId,
                    data.ReleaseDate,
                    data.ChapterTitleName,
                    data.FileSize,
                    LibraryName = _context.Library.Where(l => l.Id == s.LibraryId).Select(l => l.Name).Single(),
                    LibraryType = _context.Library.Where(l => l.Id == s.LibraryId).Select(l => l.Type).Single()
                })
            .Select(data => new ReadingListItemDto()
            {
                Id = data.ReadingListItem.Id,
                ChapterId = data.ReadingListItem.ChapterId,
                Order = data.ReadingListItem.Order,
                SeriesId = data.ReadingListItem.SeriesId,
                SeriesName = data.SeriesName,
                SeriesFormat = data.SeriesFormat,
                PagesTotal = data.TotalPages,
                ChapterNumber = data.ChapterNumber,
                VolumeNumber = data.VolumeNumber,
                LibraryId = data.LibraryId,
                VolumeId = data.VolumeId,
                ReadingListId = data.ReadingListItem.ReadingListId,
                ReleaseDate = data.ReleaseDate,
                LibraryType = data.LibraryType,
                ChapterTitleName = data.ChapterTitleName,
                LibraryName = data.LibraryName,
                FileSize = data.FileSize
            })
            .Where(o => userLibraries.Contains(o.LibraryId))
            .OrderBy(rli => rli.Order)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync();

        foreach (var item in items)
        {
            item.Title = ReadingListService.FormatTitle(item);
        }

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
            progressItem.LastReadingProgressUtc = progress.LastModifiedUtc;
        }

        return items;
    }

    public async Task<ReadingListDto?> GetReadingListDtoByIdAsync(int readingListId, int userId)
    {
        return await _context.ReadingList
            .Where(r => r.Id == readingListId && (r.AppUserId == userId || r.Promoted))
            .ProjectTo<ReadingListDto>(_mapper.ConfigurationProvider)
            .SingleOrDefaultAsync();
    }

    public async Task<IEnumerable<ReadingListItemDto>> AddReadingProgressModifiers(int userId, IList<ReadingListItemDto> items)
    {
        var chapterIds = items.Select(i => i.ChapterId).Distinct();
        var userProgress = await _context.AppUserProgresses
            .Where(p => p.AppUserId == userId && chapterIds.Contains(p.ChapterId))
            .AsNoTracking()
            .ToListAsync();

        foreach (var item in items)
        {
            var progress = userProgress.Where(p => p.ChapterId == item.ChapterId).ToList();
            if (progress.Count == 0) continue;
            item.PagesRead = progress.Sum(p => p.PagesRead);
            item.LastReadingProgressUtc = progress.Max(p => p.LastModifiedUtc);
        }

        return items;
    }

    public async Task<ReadingListDto?> GetReadingListDtoByTitleAsync(int userId, string title)
    {
        return await _context.ReadingList
            .Where(r => r.Title.Equals(title) && r.AppUserId == userId)
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
