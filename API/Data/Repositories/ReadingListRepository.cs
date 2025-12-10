using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.Person;
using API.DTOs.ReadingLists;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Extensions.QueryExtensions;
using API.Helpers;
using API.Services;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;
#nullable enable

[Flags]
public enum ReadingListIncludes
{
    None = 1,
    Items = 2,
    ItemChapter = 4,
}

public interface IReadingListRepository
{
    Task<PagedList<ReadingListDto>> GetReadingListDtosForUserAsync(int userId, bool includePromoted, UserParams userParams, bool sortByLastModified = true);
    Task<ReadingList?> GetReadingListByIdAsync(int readingListId, ReadingListIncludes includes = ReadingListIncludes.None);
    Task<IEnumerable<ReadingListItemDto>> GetReadingListItemDtosByIdAsync(int readingListId, int userId, UserParams? userParams = null);
    Task<ReadingListDto?> GetReadingListDtoByIdAsync(int readingListId, int userId);
    Task<IEnumerable<ReadingListItemDto>> AddReadingProgressModifiers(int userId, IList<ReadingListItemDto> items);
    Task<ReadingListDto?> GetReadingListDtoByTitleAsync(int userId, string title);
    Task<IEnumerable<ReadingListItem>> GetReadingListItemsByIdAsync(int readingListId);
    Task<IEnumerable<ReadingListDto>> GetReadingListDtosForSeriesAndUserAsync(int userId, int seriesId,
        bool includePromoted);
    Task<IEnumerable<ReadingListDto>> GetReadingListDtosForChapterAndUserAsync(int userId, int chapterId,
        bool includePromoted);
    void Remove(ReadingListItem item);
    void Add(ReadingList list);
    void BulkRemove(IEnumerable<ReadingListItem> items);
    void Update(ReadingList list);
    Task<int> Count();
    Task<string?> GetCoverImageAsync(int readingListId);
    Task<IList<string>> GetRandomCoverImagesAsync(int readingListId);
    Task<IList<string>> GetAllCoverImagesAsync();
    Task<bool> ReadingListExists(string name);
    Task<bool> ReadingListExistsForUser(string name, int userId);
    IEnumerable<PersonDto> GetReadingListPeopleAsync(int readingListId, PersonRole role);
    Task<ReadingListCast> GetReadingListAllPeopleAsync(int readingListId);
    Task<IList<ReadingList>> GetAllWithCoversInDifferentEncoding(EncodeFormat encodeFormat);
    Task<int> RemoveReadingListsWithoutSeries();
    Task<ReadingList?> GetReadingListByTitleAsync(string name, int userId, ReadingListIncludes includes = ReadingListIncludes.Items);
    Task<IEnumerable<ReadingList>> GetReadingListsByIds(IList<int> ids, ReadingListIncludes includes = ReadingListIncludes.Items);
    Task<IEnumerable<ReadingList>> GetReadingListsBySeriesId(int seriesId, ReadingListIncludes includes = ReadingListIncludes.Items);
    Task<ReadingListInfoDto?> GetReadingListInfoAsync(int readingListId);
    Task<bool> AnyUserReadingProgressAsync(int readingListId, int userId);
    Task<ReadingListItemDto?> GetContinueReadingPoint(int readingListId, int userId);
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
            .FirstOrDefaultAsync();
    }

    public async Task<IList<string>> GetAllCoverImagesAsync()
    {
        return (await _context.ReadingList
            .Select(t => t.CoverImage)
            .Where(t => !string.IsNullOrEmpty(t))
            .ToListAsync())!;
    }

    public async Task<IList<string>> GetRandomCoverImagesAsync(int readingListId)
    {
        var random = new Random();
        var data = await _context.ReadingList
                .Where(r => r.Id == readingListId)
                .SelectMany(r => r.Items.Select(ri => ri.Chapter.CoverImage))
                .Where(t => !string.IsNullOrEmpty(t))
                .ToListAsync();

        return data
            .OrderBy(_ => random.Next())
            .Take(4)
            .ToList();
    }


    public async Task<bool> ReadingListExists(string name)
    {
        var normalized = name.ToNormalized();
        return await _context.ReadingList
            .AnyAsync(x => x.NormalizedTitle != null && x.NormalizedTitle.Equals(normalized));
    }

    public async Task<bool> ReadingListExistsForUser(string name, int userId)
    {
        var normalized = name.ToNormalized();
        return await _context.ReadingList
            .AnyAsync(x => x.NormalizedTitle != null && x.NormalizedTitle.Equals(normalized) && x.AppUserId == userId);
    }

    public IEnumerable<PersonDto> GetReadingListPeopleAsync(int readingListId, PersonRole role)
    {
        return _context.ReadingListItem
            .Where(item => item.ReadingListId == readingListId)
            .SelectMany(item => item.Chapter.People)
            .Where(p => p.Role == role)
            .OrderBy(p => p.Person.NormalizedName)
            .Select(p => p.Person)
            .Distinct()
            .ProjectTo<PersonDto>(_mapper.ConfigurationProvider)
            .AsEnumerable();
    }

    public async Task<ReadingListCast> GetReadingListAllPeopleAsync(int readingListId)
    {
        var allPeople = await _context.ReadingListItem
            .Where(item => item.ReadingListId == readingListId)
            .SelectMany(item => item.Chapter.People)
            .OrderBy(p => p.Person.NormalizedName)
            .Select(p => new
            {
                Role = p.Role,
                Person = _mapper.Map<PersonDto>(p.Person)
            })
            .Distinct()
            .ToListAsync();

        // Create the ReadingListCast object
        var cast = new ReadingListCast();

        // Group people by role and populate the appropriate collections
        foreach (var personGroup in allPeople.GroupBy(p => p.Role))
        {
            var people = personGroup.Select(pg => pg.Person).ToList();

            switch (personGroup.Key)
            {
                case PersonRole.Writer:
                    cast.Writers = people;
                    break;
                case PersonRole.CoverArtist:
                    cast.CoverArtists = people;
                    break;
                case PersonRole.Publisher:
                    cast.Publishers = people;
                    break;
                case PersonRole.Character:
                    cast.Characters = people;
                    break;
                case PersonRole.Penciller:
                    cast.Pencillers = people;
                    break;
                case PersonRole.Inker:
                    cast.Inkers = people;
                    break;
                case PersonRole.Imprint:
                    cast.Imprints = people;
                    break;
                case PersonRole.Colorist:
                    cast.Colorists = people;
                    break;
                case PersonRole.Letterer:
                    cast.Letterers = people;
                    break;
                case PersonRole.Editor:
                    cast.Editors = people;
                    break;
                case PersonRole.Translator:
                    cast.Translators = people;
                    break;
                case PersonRole.Team:
                    cast.Teams = people;
                    break;
                case PersonRole.Location:
                    cast.Locations = people;
                    break;
                case PersonRole.Other:
                    break;
            }
        }

        return cast;
    }

    public async Task<IList<ReadingList>> GetAllWithCoversInDifferentEncoding(EncodeFormat encodeFormat)
    {
        var extension = encodeFormat.GetExtension();
        return await _context.ReadingList
            .Where(c => !string.IsNullOrEmpty(c.CoverImage) && !c.CoverImage.EndsWith(extension))
            .ToListAsync();
    }


    public async Task<int> RemoveReadingListsWithoutSeries()
    {
        var listsToDelete = await _context.ReadingList
            .Include(c => c.Items)
            .Where(c => c.Items.Count == 0)
            .AsSplitQuery()
            .ToListAsync();
        _context.RemoveRange(listsToDelete);

        return await _context.SaveChangesAsync();
    }


    public async Task<ReadingList?> GetReadingListByTitleAsync(string name, int userId, ReadingListIncludes includes = ReadingListIncludes.Items)
    {
        var normalized = name.ToNormalized();
        return await _context.ReadingList
            .Includes(includes)
            .FirstOrDefaultAsync(x => x.NormalizedTitle != null && x.NormalizedTitle.Equals(normalized) && x.AppUserId == userId);
    }

    public async Task<IEnumerable<ReadingList>> GetReadingListsByIds(IList<int> ids, ReadingListIncludes includes = ReadingListIncludes.Items)
    {
        return await _context.ReadingList
            .Where(c => ids.Contains(c.Id))
            .Includes(includes)
            .AsSplitQuery()
            .ToListAsync();
    }
    public async Task<IEnumerable<ReadingList>> GetReadingListsBySeriesId(int seriesId, ReadingListIncludes includes = ReadingListIncludes.Items)
    {
        return await _context.ReadingList
            .Where(rl => rl.Items.Any(rli => rli.SeriesId == seriesId))
            .Includes(includes)
            .AsSplitQuery()
            .ToListAsync();
    }

    /// <summary>
    /// Returns a Partial ReadingListInfoDto. The HourEstimate needs to be calculated outside the repo
    /// </summary>
    /// <param name="readingListId"></param>
    /// <returns></returns>
    public async Task<ReadingListInfoDto?> GetReadingListInfoAsync(int readingListId)
    {
        // Get sum of these across all ReadingListItems: long wordCount, int pageCount, bool isEpub (assume false if any ReadingListeItem.Series.Format is non-epub)
        var readingList = await _context.ReadingList
            .Where(rl => rl.Id == readingListId)
            .Include(rl => rl.Items)
            .ThenInclude(item => item.Series)
            .Include(rl => rl.Items)
            .ThenInclude(item => item.Volume)
            .Include(rl => rl.Items)
            .ThenInclude(item => item.Chapter)
            .Select(rl => new ReadingListInfoDto()
            {
                WordCount = rl.Items.Sum(item => item.Chapter.WordCount),
                Pages = rl.Items.Sum(item => item.Chapter.Pages),
                IsAllEpub = rl.Items.All(item => item.Series.Format == MangaFormat.Epub),
            })
            .FirstOrDefaultAsync();

        return readingList;
    }


    public void Remove(ReadingListItem item)
    {
        _context.ReadingListItem.Remove(item);
    }

    public void BulkRemove(IEnumerable<ReadingListItem> items)
    {
        _context.ReadingListItem.RemoveRange(items);
    }


    public async Task<PagedList<ReadingListDto>> GetReadingListDtosForUserAsync(int userId, bool includePromoted, UserParams userParams, bool sortByLastModified = true)
    {
        var user = await _context.AppUser.FirstAsync(u => u.Id == userId);
        var query = _context.ReadingList
            .Where(l => l.AppUserId == userId || (includePromoted &&  l.Promoted ))
            .RestrictAgainstAgeRestriction(user.GetAgeRestriction());

        query = sortByLastModified ? query.OrderByDescending(l => l.LastModified) : query.OrderBy(l => l.Title);

       var finalQuery = query.ProjectTo<ReadingListDto>(_mapper.ConfigurationProvider)
            .AsNoTracking();

       return await PagedList<ReadingListDto>.CreateAsync(finalQuery, userParams.PageNumber, userParams.PageSize);
    }

    public async Task<IEnumerable<ReadingListDto>> GetReadingListDtosForSeriesAndUserAsync(int userId, int seriesId, bool includePromoted)
    {
        var user = await _context.AppUser.FirstAsync(u => u.Id == userId);
        var query = _context.ReadingList
            .Where(l => l.AppUserId == userId || (includePromoted && l.Promoted ))
            .RestrictAgainstAgeRestriction(user.GetAgeRestriction())
            .Where(l => l.Items.Any(i => i.SeriesId == seriesId))
            .AsSplitQuery()
            .OrderBy(l => l.Title)
            .ProjectTo<ReadingListDto>(_mapper.ConfigurationProvider)
            .AsNoTracking();

        return await query.ToListAsync();
    }

    public async Task<IEnumerable<ReadingListDto>> GetReadingListDtosForChapterAndUserAsync(int userId, int chapterId, bool includePromoted)
    {
        var user = await _context.AppUser.FirstAsync(u => u.Id == userId);
        var query = _context.ReadingList
            .Where(l => l.AppUserId == userId || (includePromoted && l.Promoted ))
            .RestrictAgainstAgeRestriction(user.GetAgeRestriction())
            .Where(l => l.Items.Any(i => i.ChapterId == chapterId))
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

    public async Task<bool> AnyUserReadingProgressAsync(int readingListId, int userId)
    {
        // Since the list is already created, we can assume RBS doesn't need to apply
        var chapterIdsQuery = _context.ReadingListItem
            .Where(s => s.ReadingListId == readingListId)
            .Select(s => s.ChapterId)
            .AsQueryable();

        return await _context.AppUserProgresses
            .Where(p => chapterIdsQuery.Contains(p.ChapterId) && p.AppUserId == userId)
            .AsNoTracking()
            .AnyAsync();
    }

    public async Task<ReadingListItemDto?> GetContinueReadingPoint(int readingListId, int userId)
    {
        var userLibraries = _context.Library.GetUserLibraries(userId);

        var query = _context.ReadingListItem
            .Where(rli => rli.ReadingListId == readingListId)
            .Join(_context.Chapter, rli => rli.ChapterId, chapter => chapter.Id, (rli, chapter) => new
                {
                    ReadingListItem = rli,
                    Chapter = chapter,
                    FileSize = _context.MangaFile.Where(f => f.ChapterId == chapter.Id).Sum(f => (long?)f.Bytes) ?? 0
                })
            .Join(_context.Volume, x => x.ReadingListItem.VolumeId, volume => volume.Id, (x, volume) => new
                {
                    x.ReadingListItem,
                    x.Chapter,
                    x.FileSize,
                    Volume = volume
                })
            .Join(_context.Series, x => x.ReadingListItem.SeriesId, series => series.Id, (x, series) => new
                {
                    x.ReadingListItem,
                    x.Chapter,
                    x.Volume,
                    x.FileSize,
                    Series = series
                })
            .Where(x => userLibraries.Contains(x.Series.LibraryId))
            .GroupJoin(_context.AppUserProgresses.Where(p => p.AppUserId == userId),
                x => x.ReadingListItem.ChapterId,
                progress => progress.ChapterId,
                (x, progressGroup) => new
                {
                    x.ReadingListItem,
                    x.Chapter,
                    x.Volume,
                    x.Series,
                    x.FileSize,
                    ProgressGroup = progressGroup
                })
            .SelectMany(
                x => x.ProgressGroup.DefaultIfEmpty(),
                (x, progress) => new
                {
                    x.ReadingListItem,
                    x.Chapter,
                    x.Volume,
                    x.Series,
                    x.FileSize,
                    Progress = progress,
                    PagesRead = progress != null ? progress.PagesRead : 0,
                    HasProgress = progress != null,
                    IsPartiallyRead = progress != null && progress.PagesRead > 0 && progress.PagesRead < x.Chapter.Pages,
                    IsUnread = progress == null || progress.PagesRead == 0
                })
            .OrderBy(x => x.ReadingListItem.Order);

        // First try to find a partially read item then the first unread item
        var item = await query
            .OrderBy(x => x.IsPartiallyRead ? 0 : x.IsUnread ? 1 : 2)
            .ThenBy(x => x.ReadingListItem.Order)
            .FirstOrDefaultAsync();


        if (item == null) return null;

        // Map to DTO
        var library = await _context.Library
            .Where(l => l.Id == item.Series.LibraryId)
            .Select(l => new { l.Name, l.Type })
            .FirstAsync();

        var dto = new ReadingListItemDto
        {
            Id = item.ReadingListItem.Id,
            ChapterId = item.ReadingListItem.ChapterId,
            Order = item.ReadingListItem.Order,
            SeriesId = item.ReadingListItem.SeriesId,
            SeriesName = item.Series.Name,
            SeriesSortName = item.Series.SortName,
            SeriesFormat = item.Series.Format,
            PagesTotal = item.Chapter.Pages,
            PagesRead = item.PagesRead,
            ChapterNumber = item.Chapter.Range,
            VolumeNumber = item.Volume.Name,
            LibraryId = item.Series.LibraryId,
            VolumeId = item.Volume.Id,
            ReadingListId = item.ReadingListItem.ReadingListId,
            ReleaseDate = item.Chapter.ReleaseDate,
            LibraryType = library.Type,
            ChapterTitleName = item.Chapter.TitleName,
            LibraryName = library.Name,
            FileSize = item.FileSize,
            Summary = item.Chapter.Summary,
            IsSpecial = item.Chapter.IsSpecial,
            LastReadingProgressUtc = item.Progress?.LastModifiedUtc
        };

        dto.Title = ReadingListService.FormatTitle(dto);

        return dto;
    }


    public async Task<IEnumerable<ReadingListItemDto>> GetReadingListItemDtosByIdAsync(int readingListId, int userId, UserParams? userParams = null)
    {
        var userLibraries = _context.Library.GetUserLibraries(userId);

        var query = _context.ReadingListItem
        .Where(s => s.ReadingListId == readingListId)
        .Join(_context.Chapter,
            s => s.ChapterId,
            chapter => chapter.Id,
            (data, chapter) => new
            {
                TotalPages = chapter.Pages,
                ChapterNumber = chapter.Range,
                chapter.ReleaseDate,
                ReadingListItem = data,
                ChapterTitleName = chapter.TitleName,
                FileSize = chapter.Files.Sum(f => f.Bytes),
                chapter.Summary,
                chapter.IsSpecial
            })
        .Join(_context.Volume,
            s => s.ReadingListItem.VolumeId,
            volume => volume.Id,
            (data, volume) => new
            {
                data.ReadingListItem,
                data.TotalPages,
                data.ChapterNumber,
                data.ReleaseDate,
                data.ChapterTitleName,
                data.FileSize,
                data.Summary,
                data.IsSpecial,
                VolumeId = volume.Id,
                VolumeNumber = volume.Name,
            })
        .Join(_context.Series,
            s => s.ReadingListItem.SeriesId,
            series => series.Id,
            (data, s) => new
            {
                SeriesName = s.Name,
                SortName = s.SortName,
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
                data.Summary,
                data.IsSpecial,
                LibraryName = _context.Library.Where(l => l.Id == s.LibraryId).Select(l => l.Name).Single(),
                LibraryType = _context.Library.Where(l => l.Id == s.LibraryId).Select(l => l.Type).Single()
            })
        .GroupJoin(_context.AppUserProgresses.Where(p => p.AppUserId == userId),
            data => data.ReadingListItem.ChapterId,
            progress => progress.ChapterId,
            (data, progressGroup) => new { Data = data, ProgressGroup = progressGroup })
        .SelectMany(
            x => x.ProgressGroup.DefaultIfEmpty(),
            (x, progress) => new ReadingListItemDto()
            {
                Id = x.Data.ReadingListItem.Id,
                ChapterId = x.Data.ReadingListItem.ChapterId,
                Order = x.Data.ReadingListItem.Order,
                SeriesId = x.Data.ReadingListItem.SeriesId,
                SeriesName = x.Data.SeriesName,
                SeriesSortName = x.Data.SortName,
                SeriesFormat = x.Data.SeriesFormat,
                PagesTotal = x.Data.TotalPages,
                ChapterNumber = x.Data.ChapterNumber,
                VolumeNumber = x.Data.VolumeNumber,
                LibraryId = x.Data.LibraryId,
                VolumeId = x.Data.VolumeId,
                ReadingListId = x.Data.ReadingListItem.ReadingListId,
                ReleaseDate = x.Data.ReleaseDate,
                LibraryType = x.Data.LibraryType,
                ChapterTitleName = x.Data.ChapterTitleName,
                LibraryName = x.Data.LibraryName,
                FileSize = x.Data.FileSize,
                Summary = x.Data.Summary,
                IsSpecial = x.Data.IsSpecial,
                PagesRead = progress != null ? progress.PagesRead : 0,
                LastReadingProgressUtc = progress != null ? progress.LastModifiedUtc : null
            })
        .Where(o => userLibraries.Contains(o.LibraryId))
        .OrderBy(rli => rli.Order)
        .AsSplitQuery();

        if (userParams != null)
        {
            query = query
                .Skip((userParams.PageNumber - 1) * userParams.PageSize)
                .Take(userParams.PageSize);
        }

        var items = await query.ToListAsync();

        foreach (var item in items)
        {
            item.Title = ReadingListService.FormatTitle(item);
        }

        return items;
    }

    public async Task<ReadingListDto?> GetReadingListDtoByIdAsync(int readingListId, int userId)
    {
        var user = await _context.AppUser.FirstAsync(u => u.Id == userId);
        return await _context.ReadingList
            .Where(r => r.Id == readingListId && (r.AppUserId == userId || r.Promoted))
            .RestrictAgainstAgeRestriction(user.GetAgeRestriction())
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
