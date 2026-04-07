using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Kavita.API.Repositories;
using Kavita.Common.Extensions;
using Kavita.Database.Extensions;
using Kavita.Models.Constants;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Metadata;
using Kavita.Models.DTOs.Reader;
using Kavita.Models.DTOs.SeriesDetail;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Metadata;
using Kavita.Models.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Repositories;


public class ChapterRepository(DataContext context, IMapper mapper) : IChapterRepository
{
    public void Update(Chapter chapter)
    {
        context.Entry(chapter).State = EntityState.Modified;
    }

    public void Remove(Chapter chapter)
    {
        context.Chapter.Remove(chapter);
    }

    public void Remove(IList<Chapter> chapters)
    {
        context.Chapter.RemoveRange(chapters);
    }

    public async Task<IList<Chapter>> GetChaptersByIdsAsync(IList<int> chapterIds,
        ChapterIncludes includes = ChapterIncludes.None, CancellationToken ct = default)
    {
        return await context.Chapter
            .Where(c => chapterIds.Contains(c.Id))
            .Includes(includes)
            .AsSplitQuery()
            .ToListAsync(ct);
    }

    /// <summary>
    /// Populates a partial IChapterInfoDto
    /// </summary>
    /// <returns></returns>
    public async Task<IChapterInfoDto?> GetChapterInfoDtoAsync(int chapterId, CancellationToken ct = default)
    {
        var chapterInfo = await context.Chapter
            .Where(c => c.Id == chapterId)
            .Join(context.Volume, c => c.VolumeId, v => v.Id, (chapter, volume) => new
            {
                ChapterNumber = chapter.MinNumber,
                VolumeNumber = volume.Name,
                VolumeId = volume.Id,
                chapter.IsSpecial,
                chapter.TitleName,
                volume.SeriesId,
                chapter.Pages,
            })
            .Join(context.Series, data => data.SeriesId, series => series.Id, (data, series) => new
            {
                data.ChapterNumber,
                data.VolumeNumber,
                data.VolumeId,
                data.IsSpecial,
                data.SeriesId,
                data.Pages,
                data.TitleName,
                SeriesFormat = series.Format,
                SeriesName = series.Name,
                series.LibraryId,
                LibraryType = series.Library.Type
            })
            .Select(data => new ChapterInfoDto()
            {
                ChapterNumber = data.ChapterNumber + string.Empty,
                VolumeNumber = data.VolumeNumber + string.Empty,
                VolumeId = data.VolumeId,
                IsSpecial = data.IsSpecial,
                SeriesId = data.SeriesId,
                SeriesFormat = data.SeriesFormat,
                SeriesName = data.SeriesName,
                LibraryId = data.LibraryId,
                Pages = data.Pages,
                ChapterTitle = data.TitleName,
                LibraryType = data.LibraryType
            })
            .AsNoTracking()
            .AsSplitQuery()
            .SingleOrDefaultAsync(ct);

        return chapterInfo;
    }

    public Task<int> GetChapterTotalPagesAsync(int chapterId, CancellationToken ct = default)
    {
        return context.Chapter
            .Where(c => c.Id == chapterId)
            .Select(c => c.Pages)
            .FirstOrDefaultAsync(ct);
    }
    public async Task<ChapterDto?> GetChapterDtoAsync(int chapterId, int userId, CancellationToken ct = default)
    {
        var chapter = await context.Chapter
            .Includes(ChapterIncludes.Files | ChapterIncludes.People)
            .ProjectToWithProgress<Chapter, ChapterDto>(mapper, userId)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == chapterId, ct);

        return chapter;
    }

    public async Task<IList<ChapterDto>> GetChapterDtoByIdsAsync(IEnumerable<int> chapterIds, int userId,
        CancellationToken ct = default)
    {
        var chapters = await context.Chapter
                .Where(c => chapterIds.Contains(c.Id))
                .Includes(ChapterIncludes.Files | ChapterIncludes.People)
                .ProjectToWithProgress<Chapter, ChapterDto>(mapper, userId)
                .AsSplitQuery()
                .ToListAsync(ct) ;

        return chapters;
    }

    public async Task<ChapterMetadataDto?> GetChapterMetadataDtoAsync(int chapterId,
        ChapterIncludes includes = ChapterIncludes.Files, CancellationToken ct = default)
    {
        var chapter = await context.Chapter
            .Includes(includes)
            .ProjectTo<ChapterMetadataDto>(mapper.ConfigurationProvider)
            .AsNoTracking()
            .AsSplitQuery()
            .SingleOrDefaultAsync(c => c.Id == chapterId, ct);

        return chapter;
    }

    /// <summary>
    /// Returns non-tracked files for a given chapterId
    /// </summary>
    /// <param name="chapterId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<IList<MangaFile>> GetFilesForChapterAsync(int chapterId, CancellationToken ct = default)
    {
        return await context.MangaFile
            .Where(c => chapterId == c.ChapterId)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    /// <summary>
    /// Returns a Chapter for an id. Includes linked <see cref="MangaFile"/>s.
    /// </summary>
    /// <param name="chapterId"></param>
    /// <param name="includes"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<Chapter?> GetChapterAsync(int chapterId, ChapterIncludes includes = ChapterIncludes.Files,
        CancellationToken ct = default)
    {
        return await context.Chapter
            .Includes(includes)
            .OrderBy(c => c.SortOrder)
            .FirstOrDefaultAsync(c => c.Id == chapterId, ct);
    }

    /// <summary>
    /// Returns Chapters for a volume id.
    /// </summary>
    /// <param name="volumeId"></param>
    /// <param name="includes"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<IList<Chapter>> GetChaptersAsync(int volumeId, ChapterIncludes includes = ChapterIncludes.None,
        CancellationToken ct = default)
    {
        return await context.Chapter
            .Where(c => c.VolumeId == volumeId)
            .Includes(includes)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Returns Chapters for a volume id with Progress
    /// </summary>
    /// <param name="volumeId"></param>
    /// <param name="userId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<IList<ChapterDto>> GetChapterDtosAsync(int volumeId, int userId, CancellationToken ct = default)
    {
        return await context.Chapter
            .Where(c => c.VolumeId == volumeId)
            .Includes(ChapterIncludes.Files | ChapterIncludes.People)
            .OrderBy(c => c.SortOrder)
            .ProjectToWithProgress<Chapter, ChapterDto>(mapper, userId)
            .ToListAsync(ct);
    }


    /// <summary>
    /// Returns the cover image for a chapter id.
    /// </summary>
    /// <param name="chapterId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<string?> GetChapterCoverImageAsync(int chapterId, CancellationToken ct = default)
    {
        return await context.Chapter
            .Where(c => c.Id == chapterId)
            .Select(c => c.CoverImage)
            .SingleOrDefaultAsync(ct);
    }

    public async Task<IList<string>> GetAllCoverImagesAsync(CancellationToken ct = default)
    {
        return (await context.Chapter
            .Select(c => c.CoverImage)
            .Where(t => !string.IsNullOrEmpty(t))
            .ToListAsync(ct))!;
    }

    public async Task<IList<Chapter>> GetAllChaptersWithCoversInDifferentEncoding(EncodeFormat format,
        CancellationToken ct = default)
    {
        var extension = format.GetExtension();
        return await context.Chapter
            .Where(c => !string.IsNullOrEmpty(c.CoverImage)  && !c.CoverImage.EndsWith(extension))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Returns cover images for locked chapters
    /// </summary>
    /// <returns></returns>
    public async Task<IEnumerable<string>> GetCoverImagesForLockedChaptersAsync(CancellationToken ct = default)
    {
        return (await context.Chapter
            .Where(c => c.CoverImageLocked)
            .Select(c => c.CoverImage)
            .Where(t => !string.IsNullOrEmpty(t))
            .ToListAsync(ct))!;
    }

    /// <summary>
    /// Returns non-tracked files for a set of <paramref name="chapterIds"/>
    /// </summary>
    /// <param name="chapterIds">List of chapter Ids</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<IList<MangaFile>> GetFilesForChaptersAsync(IReadOnlyList<int> chapterIds,
        CancellationToken ct = default)
    {
        return await context.MangaFile
            .Where(c => chapterIds.Contains(c.ChapterId))
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<long> GetFilesizeAsync(int chapterId, CancellationToken ct = default)
    {
        return await context.MangaFile
            .Where(c => c.ChapterId == chapterId)
            .SumAsync(c => c.Bytes, cancellationToken: ct);
    }

    public async Task<Dictionary<int, long>> GetFilesizesAsync(IList<int> chapterIds, CancellationToken ct = default)
    {
        return await chapterIds.BatchToDictionaryAsync(50, batch =>
            context.MangaFile
                .Where(f => batch.Contains(f.ChapterId))
                .ToDictionaryAsync(f => f.ChapterId, f => f.Bytes, cancellationToken: ct));
    }

    /// <summary>
    /// Includes Volumes
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public IQueryable<Chapter> GetChaptersForSeries(int seriesId, CancellationToken ct = default)
    {
        return context.Chapter
            .Where(c => c.Volume.SeriesId == seriesId)
            .OrderBy(c => c.SortOrder)
            .Include(c => c.Volume);
    }

    public async Task<IList<Chapter>> GetAllChaptersForSeries(int seriesId, CancellationToken ct = default)
    {
        return await context.Chapter
            .Where(c => c.Volume.SeriesId == seriesId)
            .OrderBy(c => c.SortOrder)
            .Include(c => c.Volume)
            .Include(c => c.People)
            .ThenInclude(cp => cp.Person)
            .ToListAsync(ct);
    }

    public async Task<int> GetAverageUserRating(int chapterId, int userId, CancellationToken ct = default)
    {
        // If there is a 0 or 1 rating and that rating is you, return 0 back
        var countOfRatingsThatAreUser = await context.AppUserChapterRating
            .Where(r => r.ChapterId == chapterId && r.HasBeenRated)
            .CountAsync(u => u.AppUserId == userId, ct);

        if (countOfRatingsThatAreUser == 1)
        {
            return 0;
        }

        var avg = await context.AppUserChapterRating
            .Where(r => r.ChapterId == chapterId && r.HasBeenRated)
            .AverageAsync(r => (int?) r.Rating, ct);

        return avg.HasValue ? (int) (avg.Value * 20) : 0;
    }

    public async Task<IList<UserReviewDto>> GetExternalChapterReviewDtos(int chapterId, CancellationToken ct = default)
    {
        return await context.Chapter
            .Where(c => c.Id == chapterId)
            .SelectMany(c => c.ExternalReviews)
            // Don't use ProjectTo, it fails to map int to float (??)
            .Select(r => mapper.Map<UserReviewDto>(r))
            .ToListAsync(ct);
    }

    public async Task<IList<ExternalReview>> GetExternalChapterReview(int chapterId, CancellationToken ct = default)
    {
        return await context.Chapter
            .Where(c => c.Id == chapterId)
            .SelectMany(c => c.ExternalReviews)
            .ToListAsync(ct);
    }

    public async Task<IList<RatingDto>> GetExternalChapterRatingDtos(int chapterId, CancellationToken ct = default)
    {
        return await context.Chapter
            .Where(c => c.Id == chapterId)
            .SelectMany(c => c.ExternalRatings)
            .ProjectTo<RatingDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    public async Task<IList<ExternalRating>> GetExternalChapterRatings(int chapterId, CancellationToken ct = default)
    {
        return await context.Chapter
            .Where(c => c.Id == chapterId)
            .SelectMany(c => c.ExternalRatings)
            .ToListAsync(ct);
    }

    public async Task<ChapterDto?> GetCurrentlyReadingChapterAsync(int seriesId, int userId, CancellationToken ct = default)
    {
        var chapterWithProgress = await context.AppUserProgresses
            .Where(p => p.AppUserId == userId)
            .Join(
                context.Chapter
                    .Include(c => c.Volume)
                    .Include(c => c.Files),
                p => p.ChapterId,
                c => c.Id,
                (p, c) => new { Chapter = c, p.PagesRead }
            )
            .Where(x => x.Chapter.Volume.SeriesId == seriesId)
            .Where(x => x.Chapter.Volume.Number != ParserConstants.LooseLeafVolumeNumber)
            .Where(x => x.PagesRead > 0 && x.PagesRead < x.Chapter.Pages)
            .OrderBy(x => x.Chapter.Volume.Number)
            .ThenBy(x => x.Chapter.SortOrder)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        if (chapterWithProgress == null) return null;

        // Map chapter to DTO
        var dto = mapper.Map<ChapterDto>(chapterWithProgress.Chapter);
        dto.PagesRead = chapterWithProgress.PagesRead;

        return dto;
    }

    public async Task<ChapterDto?> GetFirstChapterForSeriesAsync(int seriesId, int userId, CancellationToken ct = default)
    {
        // Get the chapter entity with proper ordering
        return await context.Chapter
            .Include(c => c.Volume)
            .Include(c => c.Files)
            .Where(c => c.Volume.SeriesId == seriesId)
            .ApplyDefaultChapterOrdering()
            .AsNoTracking()
            .ProjectToWithProgress<Chapter, ChapterDto>(mapper, userId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ChapterDto?> GetFirstChapterForVolumeAsync(int volumeId, int userId, CancellationToken ct = default)
    {
        // Get the chapter entity with proper ordering
        return await context.Chapter
            .Include(c => c.Volume)
            .Include(c => c.Files)
            .Where(c => c.Volume.Id == volumeId)
            .ApplyDefaultChapterOrdering()
            .AsNoTracking()
            .ProjectToWithProgress<Chapter, ChapterDto>(mapper, userId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IList<ChapterDto>> GetChapterDtosAsync(IEnumerable<int> chapterIds, int userId,
        CancellationToken ct = default)
    {
        var chapterIdList = chapterIds.ToList();
        if (chapterIdList.Count == 0) return [];

        return await context.Chapter
            .Where(c => chapterIdList.Contains(c.Id))
            .ProjectToWithProgress<Chapter, ChapterDto>(mapper, userId)
            .ToListAsync(ct);
    }

    public async Task<int?> GetSeriesIdForChapter(int chapterId, CancellationToken ct = default)
    {
        return await context.Chapter
            .Where(chp => chp.Id == chapterId)
            .Select(chp => chp.Volume.SeriesId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IList<Chapter>> GetChaptersByExternalIdsAsync(IList<int> kavitaIds, IList<string> comicVineIds,
        IList<long> metronIds, IList<int> libraryIds, CancellationToken ct = default)
    {
        if (comicVineIds.Count == 0 && metronIds.Count == 0 && kavitaIds.Count == 0) return [];

        var results = new List<Chapter>();
        const int chunkSize = 500;

        foreach (var batch in kavitaIds.Chunk(chunkSize))
        {
            var batchList = batch.ToList();
            results.AddRange(await BaseQuery()
                .Where(c => batchList.Contains(c.Id))
                .ToListAsync(ct));
        }

        foreach (var batch in comicVineIds.Chunk(chunkSize))
        {
            var batchList = batch.ToList();
            results.AddRange(await BaseQuery()
                .Where(c => c.ComicVineId != null && batchList.Contains(c.ComicVineId))
                .ToListAsync(ct));
        }

        foreach (var batch in metronIds.Chunk(chunkSize))
        {
            var batchList = batch.ToList();
            results.AddRange(await BaseQuery()
                .Where(c => c.MetronId > 0 && batchList.Contains(c.MetronId))
                .ToListAsync(ct));
        }

        // Dedupe as a chapter could match on multiple providers
        return results.DistinctBy(c => c.Id).ToList();

        IQueryable<Chapter> BaseQuery() => context.Chapter
            .Include(c => c.Volume)
            .ThenInclude(v => v.Series)
            .Where(c => libraryIds.Contains(c.Volume.Series.LibraryId));
    }

    public async Task<IList<Chapter>> GetChaptersByAlternateSeriesAsync(IList<string> normalizedNames, IList<int> libraryIds, CancellationToken ct = default)
    {
        if (normalizedNames.Count == 0) return [];

        // AlternateSeries is rare and not normalized in the DB, so fetch all non-empty ones and filter in-memory
        var chapters = await context.Chapter
            .Include(c => c.Volume)
            .ThenInclude(v => v.Series)
            .Where(c => libraryIds.Contains(c.Volume.Series.LibraryId))
            .Where(c => c.AlternateSeries != null && c.AlternateSeries != string.Empty)
            .ToListAsync(ct);

        var normalizedSet = new HashSet<string>(normalizedNames);
        return chapters
            .Where(c => normalizedSet.Contains(c.AlternateSeries.ToNormalized()))
            .ToList();
    }
}
