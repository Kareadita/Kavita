using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Kavita.API.Repositories;
using Kavita.Common.Extensions;
using Kavita.Common.Helpers;
using Kavita.Database.Extensions;
using Kavita.Models.DTOs.Person;
using Kavita.Models.DTOs.ReadingLists;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.ReadingList;
using Kavita.Models.Entities.ReadingLists;
using Kavita.Models.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Repositories;

public class ReadingListRepository(DataContext context, IMapper mapper) : IReadingListRepository
{
    public void Update(ReadingList list)
    {
        context.Entry(list).State = EntityState.Modified;
    }

    public void Add(ReadingList list)
    {
        context.Add(list);
    }

    public async Task<int> Count(CancellationToken ct = default)
    {
        return await context.ReadingList.CountAsync(ct);
    }

    public async Task<string?> GetCoverImageAsync(int readingListId, CancellationToken ct = default)
    {
        return await context.ReadingList
            .Where(c => c.Id == readingListId)
            .Select(c => c.CoverImage)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IList<string>> GetAllCoverImagesAsync(CancellationToken ct = default)
    {
        return (await context.ReadingList
            .Select(t => t.CoverImage)
            .Where(t => !string.IsNullOrEmpty(t))
            .ToListAsync(ct))!;
    }

    public async Task<IList<string>> GetRandomCoverImagesAsync(int readingListId, CancellationToken ct = default)
    {
        var random = new Random();
        var data = await context.ReadingList
                .Where(r => r.Id == readingListId)
                .SelectMany(r => r.Items.Select(ri => ri.Chapter.CoverImage))
                .Where(t => !string.IsNullOrEmpty(t))
                .ToListAsync(ct);

        return data
            .OrderBy(_ => random.Next())
            .Take(4)
            .ToList();
    }


    public async Task<bool> ReadingListExists(string name, int? readingListId = null,  CancellationToken ct = default)
    {
        var normalized = name.ToNormalized();

        return await context.ReadingList
            .WhereIf(readingListId != null, x => x.Id != readingListId)
            .AnyAsync(x => x.NormalizedTitle != null && x.NormalizedTitle.Equals(normalized), ct);
    }

    public async Task<bool> ReadingListExistsForUser(string name, int userId, CancellationToken ct = default)
    {
        var normalized = name.ToNormalized();
        return await context.ReadingList
            .AnyAsync(x => x.NormalizedTitle != null && x.NormalizedTitle.Equals(normalized) && x.AppUserId == userId, ct);
    }

    public IEnumerable<PersonDto> GetReadingListPeopleAsync(int readingListId, PersonRole role,
        CancellationToken ct = default)
    {
        return context.ReadingListItem
            .Where(item => item.ReadingListId == readingListId)
            .SelectMany(item => item.Chapter.People)
            .Where(p => p.Role == role)
            .OrderBy(p => p.Person.NormalizedName)
            .Select(p => p.Person)
            .Distinct()
            .ProjectTo<PersonDto>(mapper.ConfigurationProvider)
            .AsEnumerable();
    }

    public async Task<ReadingListCast> GetReadingListAllPeopleAsync(int readingListId, CancellationToken ct = default)
    {
        var allPeople = await context.ReadingListItem
            .Where(item => item.ReadingListId == readingListId)
            .SelectMany(item => item.Chapter.People)
            .OrderBy(p => p.Person.NormalizedName)
            .Select(p => new
            {
                p.Role,
                Person = mapper.Map<PersonDto>(p.Person)
            })
            .Distinct()
            .ToListAsync(ct);

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

    public async Task<IList<ReadingList>> GetAllWithCoversInDifferentEncoding(EncodeFormat encodeFormat,
        CancellationToken ct = default)
    {
        var extension = encodeFormat.GetExtension();
        return await context.ReadingList
            .Where(c => !string.IsNullOrEmpty(c.CoverImage) && !c.CoverImage.EndsWith(extension))
            .ToListAsync(ct);
    }


    public async Task<int> RemoveReadingListsWithoutSeries(CancellationToken ct = default)
    {
        var listsToDelete = await context.ReadingList
            .Include(c => c.Items)
            .Where(c => c.Items.Count == 0)
            .AsSplitQuery()
            .ToListAsync(ct);
        context.RemoveRange(listsToDelete);

        return await context.SaveChangesAsync(ct);
    }


    public async Task<ReadingList?> GetReadingListByTitleAsync(string name, int userId,
        ReadingListIncludes includes = ReadingListIncludes.Items, CancellationToken ct = default)
    {
        var normalized = name.ToNormalized();
        return await context.ReadingList
            .Includes(includes)
            .FirstOrDefaultAsync(x => x.NormalizedTitle != null && x.NormalizedTitle.Equals(normalized) && x.AppUserId == userId, ct);
    }

    public async Task<IEnumerable<ReadingList>> GetReadingListsByIds(IList<int> ids,
        ReadingListIncludes includes = ReadingListIncludes.Items, CancellationToken ct = default)
    {
        return await context.ReadingList
            .Where(c => ids.Contains(c.Id))
            .Includes(includes)
            .AsSplitQuery()
            .ToListAsync(ct);
    }
    public async Task<IEnumerable<ReadingList>> GetReadingListsBySeriesId(int seriesId,
        ReadingListIncludes includes = ReadingListIncludes.Items, CancellationToken ct = default)
    {
        return await context.ReadingList
            .Where(rl => rl.Items.Any(rli => rli.SeriesId == seriesId))
            .Includes(includes)
            .AsSplitQuery()
            .ToListAsync(ct);
    }

    /// <summary>
    /// Returns a Partial ReadingListInfoDto. The HourEstimate needs to be calculated outside the repo
    /// </summary>
    /// <param name="readingListId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<ReadingListInfoDto?> GetReadingListInfoAsync(int readingListId, CancellationToken ct = default)
    {
        // Get the sum of these across all ReadingListItems: long wordCount, int pageCount, bool isEpub (assume false if any ReadingListItem.Series.Format is non-epub)
        var readingList = await context.ReadingList
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
            .FirstOrDefaultAsync(ct);

        return readingList;
    }


    public void Remove(ReadingListItem item)
    {
        context.ReadingListItem.Remove(item);
    }

    public void BulkRemove(IEnumerable<ReadingListItem> items)
    {
        context.ReadingListItem.RemoveRange(items);
    }


    public async Task<PagedList<ReadingListDto>> GetReadingListDtosForUserAsync(int userId, bool includePromoted,
        UserParams userParams, bool sortByLastModified = true, CancellationToken ct = default)
    {
        var user = await context.AppUser.FirstAsync(u => u.Id == userId, ct);
        var query = context.ReadingList
            .Where(l => l.AppUserId == userId || (includePromoted &&  l.Promoted ))
            .RestrictAgainstAgeRestriction(user.GetAgeRestriction());

        query = sortByLastModified ? query.OrderByDescending(l => l.LastModified) : query.OrderBy(l => l.Title);

       var finalQuery = query.ProjectTo<ReadingListDto>(mapper.ConfigurationProvider)
            .AsNoTracking();

       return await PagedList<ReadingListDto>.CreateAsync(finalQuery, userParams.PageNumber, userParams.PageSize, ct);
    }

    public async Task<IEnumerable<ReadingListDto>> GetReadingListDtosForSeriesAndUserAsync(int userId, int seriesId,
        bool includePromoted, CancellationToken ct = default)
    {
        var user = await context.AppUser.FirstAsync(u => u.Id == userId, ct);
        var query = context.ReadingList
            .Where(l => l.AppUserId == userId || (includePromoted && l.Promoted ))
            .RestrictAgainstAgeRestriction(user.GetAgeRestriction())
            .Where(l => l.Items.Any(i => i.SeriesId == seriesId))
            .AsSplitQuery()
            .OrderBy(l => l.Title)
            .ProjectTo<ReadingListDto>(mapper.ConfigurationProvider)
            .AsNoTracking();

        return await query.ToListAsync(ct);
    }

    public async Task<IEnumerable<ReadingListDto>> GetReadingListDtosForChapterAndUserAsync(int userId, int chapterId,
        bool includePromoted, CancellationToken ct = default)
    {
        var ageRestriction = await context.AppUser.GetUserAgeRestriction(userId, ct: ct);

        var query = context.ReadingList
            .Where(l => l.AppUserId == userId || (includePromoted && l.Promoted ))
            .RestrictAgainstAgeRestriction(ageRestriction)
            .Where(l => l.Items.Any(i => i.ChapterId == chapterId))
            .AsSplitQuery()
            .OrderBy(l => l.Title)
            .ProjectTo<ReadingListDto>(mapper.ConfigurationProvider)
            .AsNoTracking();

        return await query.ToListAsync(ct);
    }

    public async Task<ReadingList?> GetReadingListByIdAsync(int readingListId,
        ReadingListIncludes includes = ReadingListIncludes.None, CancellationToken ct = default)
    {
        return await context.ReadingList
            .Where(r => r.Id == readingListId)
            .Includes(includes)
            .Include(r => r.Items.OrderBy(item => item.Order))
            .AsSplitQuery()
            .SingleOrDefaultAsync(ct);
    }

    public async Task<bool> AnyUserReadingProgressAsync(int readingListId, int userId, CancellationToken ct = default)
    {
        // Since the list is already created, we can assume RBS doesn't need to apply
        var chapterIdsQuery = context.ReadingListItem
            .Where(s => s.ReadingListId == readingListId)
            .Select(s => s.ChapterId)
            .AsQueryable();

        return await context.AppUserProgresses
            .Where(p => chapterIdsQuery.Contains(p.ChapterId) && p.AppUserId == userId)
            .AsNoTracking()
            .AnyAsync(ct);
    }

    public async Task<ReadingListItemDto?> GetContinueReadingPoint(int readingListId, int userId,
        CancellationToken ct = default)
    {
        var userLibraries = context.Library.GetUserLibraries(userId);

        var query = context.ReadingListItem
            .Where(rli => rli.ReadingListId == readingListId)
            .Join(context.Chapter, rli => rli.ChapterId, chapter => chapter.Id, (rli, chapter) => new
                {
                    ReadingListItem = rli,
                    Chapter = chapter,
                    FileSize = context.MangaFile.Where(f => f.ChapterId == chapter.Id).Sum(f => (long?)f.Bytes) ?? 0
                })
            .Join(context.Volume, x => x.ReadingListItem.VolumeId, volume => volume.Id, (x, volume) => new
                {
                    x.ReadingListItem,
                    x.Chapter,
                    x.FileSize,
                    Volume = volume
                })
            .Join(context.Series, x => x.ReadingListItem.SeriesId, series => series.Id, (x, series) => new
                {
                    x.ReadingListItem,
                    x.Chapter,
                    x.Volume,
                    x.FileSize,
                    Series = series
                })
            .Where(x => userLibraries.Contains(x.Series.LibraryId))
            .GroupJoin(context.AppUserProgresses.Where(p => p.AppUserId == userId),
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

        // First try to find a partially read item, then the first unread item
        var item = await query
            .OrderBy(x => x.IsPartiallyRead ? 0 : x.IsUnread ? 1 : 2)
            .ThenBy(x => x.ReadingListItem.Order)
            .FirstOrDefaultAsync(ct);


        if (item == null) return null;

        // Map to DTO
        var library = await context.Library
            .Where(l => l.Id == item.Series.LibraryId)
            .Select(l => new { l.Name, l.Type })
            .FirstAsync(ct);

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

        return dto;
    }

    public Task<int> GetReadingListItemCountAsync(int readingListId, int userId, CancellationToken ct = default)
    {
        return context.ReadingListItem.Where(rli => rli.ReadingListId == readingListId).CountAsync(ct);
    }

    public async Task<long> GetFilesizeAsync(int readingListId, int userId, CancellationToken ct = default)
    {
        var ageRestriction = await context.AppUser.GetUserAgeRestriction(userId, ct: ct);

        return await context.ReadingList
            .Where(l => l.Id == readingListId && (l.AppUserId == userId || l.Promoted))
            .RestrictAgainstAgeRestriction(ageRestriction)
            .SelectMany(l => l.Items)
            .SelectMany(i => i.Chapter.Files)
            .SumAsync(f => f.Bytes, ct);
    }

    public async Task<Dictionary<int, long>> GetFilesizesAsync(IList<int> readingListIds, int userId, CancellationToken ct = default)
    {
        var ageRestriction = await context.AppUser.GetUserAgeRestriction(userId, ct: ct);

        return await readingListIds.BatchToDictionaryAsync(50, batch =>
            context.ReadingList
                .Where(l => batch.Contains(l.Id) && (l.AppUserId == userId || l.Promoted))
                .RestrictAgainstAgeRestriction(ageRestriction)
                .Select(l => new
                {
                    l.Id,
                    Bytes = l.Items
                        .SelectMany(i => i.Chapter.Files)
                        .Sum(f => f.Bytes)
                })
                .ToDictionaryAsync(
                    x => x.Id,
                    x => x.Bytes,
                    cancellationToken: ct));
    }


    public async Task<IList<ReadingListItemDto>> GetReadingListItemDtosByIdAsync(int readingListId, int userId,
        UserParams? userParams = null, CancellationToken ct = default)
    {
        var userLibraries = context.Library.GetUserLibraries(userId);

        var query = context.ReadingListItem
            .Where(rli => rli.ReadingListId == readingListId)
            .Where(rli => userLibraries.Contains(rli.Series.LibraryId))
            .OrderBy(rli => rli.Order)
            .ProjectToWithProgress<ReadingListItem, ReadingListItemDto>(mapper, userId)
            .AsSplitQuery();

        if (userParams != null)
        {
            query = query
                .Skip((userParams.PageNumber - 1) * userParams.PageSize)
                .Take(userParams.PageSize);
        }

        return await query.ToListAsync(ct);
    }

    public async Task<ReadingListDto?> GetReadingListDtoByIdAsync(int readingListId, int userId,
        CancellationToken ct = default)
    {
        var user = await context.AppUser.FirstAsync(u => u.Id == userId, ct);

        return await context.ReadingList
            .Where(r => r.Id == readingListId && (r.AppUserId == userId || r.Promoted))
            .RestrictAgainstAgeRestriction(user.GetAgeRestriction())
            .ProjectTo<ReadingListDto>(mapper.ConfigurationProvider)
            .SingleOrDefaultAsync(ct);
    }


    public async Task<ReadingListDto?> GetReadingListDtoByTitleAsync(int userId, string title,
        CancellationToken ct = default)
    {
        return await context.ReadingList
            .Where(r => r.Title.Equals(title) && r.AppUserId == userId)
            .ProjectTo<ReadingListDto>(mapper.ConfigurationProvider)
            .SingleOrDefaultAsync(ct);
    }

    public async Task<IEnumerable<ReadingListItem>> GetReadingListItemsByIdAsync(int readingListId,
        CancellationToken ct = default)
    {
        return await context.ReadingListItem
            .Where(r => r.ReadingListId == readingListId)
            .OrderBy(r => r.Order)
            .ToListAsync(ct);
    }


    public async Task<Dictionary<int, List<int>>> GetSyncableReadingListsAsync(DateTime lastCheckThreshold, CancellationToken ct = default)
    {
        return await context.ReadingList
            .Where(rl =>
                rl.Provider == ReadingListProvider.Url
                && (!string.IsNullOrEmpty(rl.SourcePath) || !string.IsNullOrEmpty(rl.DownloadUrl)) // Source Path for GH, Download Url for ProAdd
                && (rl.LastSyncCheckUtc == null || rl.LastSyncCheckUtc < lastCheckThreshold))
            .GroupBy(rl => rl.AppUserId)
            .ToDictionaryAsync(g => g.Key, g => g.Select(rl => rl.Id).ToList(), ct);
    }
}
