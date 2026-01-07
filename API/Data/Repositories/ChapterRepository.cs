using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.DTOs.Metadata;
using API.DTOs.Reader;
using API.DTOs.SeriesDetail;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Extensions;
using API.Extensions.QueryExtensions;
using API.Services.Tasks.Scanner.Parser;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;
#nullable enable

[Flags]
public enum ChapterIncludes
{
    None = 1,
    Volumes = 2,
    Files = 4,
    People = 8,
    Genres = 16,
    Tags = 32,
    ExternalReviews = 1 << 6,
    ExternalRatings = 1 << 7
}

public interface IChapterRepository
{
    void Update(Chapter chapter);
    void Remove(Chapter chapter);
    void Remove(IList<Chapter> chapters);
    Task<IEnumerable<Chapter>> GetChaptersByIdsAsync(IList<int> chapterIds, ChapterIncludes includes = ChapterIncludes.None);
    Task<IChapterInfoDto?> GetChapterInfoDtoAsync(int chapterId);
    Task<int> GetChapterTotalPagesAsync(int chapterId);
    Task<Chapter?> GetChapterAsync(int chapterId, ChapterIncludes includes = ChapterIncludes.Files);
    Task<ChapterDto?> GetChapterDtoAsync(int chapterId, int userId);
    Task<IList<ChapterDto>> GetChapterDtoByIdsAsync(IEnumerable<int> chapterIds, int userId);
    Task<ChapterMetadataDto?> GetChapterMetadataDtoAsync(int chapterId, ChapterIncludes includes = ChapterIncludes.Files);
    Task<IList<MangaFile>> GetFilesForChapterAsync(int chapterId);
    Task<IList<Chapter>> GetChaptersAsync(int volumeId, ChapterIncludes includes = ChapterIncludes.None);
    Task<IList<ChapterDto>> GetChapterDtosAsync(int volumeId, int userId);
    Task<IList<MangaFile>> GetFilesForChaptersAsync(IReadOnlyList<int> chapterIds);
    Task<string?> GetChapterCoverImageAsync(int chapterId);
    Task<IList<string>> GetAllCoverImagesAsync();
    Task<IList<Chapter>> GetAllChaptersWithCoversInDifferentEncoding(EncodeFormat format);
    Task<IEnumerable<string>> GetCoverImagesForLockedChaptersAsync();
    IQueryable<Chapter> GetChaptersForSeries(int seriesId);
    Task<IList<Chapter>> GetAllChaptersForSeries(int seriesId);
    Task<int> GetAverageUserRating(int chapterId, int userId);
    Task<IList<UserReviewDto>> GetExternalChapterReviewDtos(int chapterId);
    Task<IList<ExternalReview>> GetExternalChapterReview(int chapterId);
    Task<IList<RatingDto>> GetExternalChapterRatingDtos(int chapterId);
    Task<IList<ExternalRating>> GetExternalChapterRatings(int chapterId);
    Task<ChapterDto?> GetCurrentlyReadingChapterAsync(int seriesId, int userId);
    Task<ChapterDto?> GetFirstChapterForSeriesAsync(int seriesId, int userId);
    Task<ChapterDto?> GetFirstChapterForVolumeAsync(int volumeId, int userId);
    Task<IList<ChapterDto>> GetChapterDtosAsync(IEnumerable<int> chapterIds, int userId);
}
public class ChapterRepository : IChapterRepository
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;

    public ChapterRepository(DataContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public void Update(Chapter chapter)
    {
        _context.Entry(chapter).State = EntityState.Modified;
    }

    public void Remove(Chapter chapter)
    {
        _context.Chapter.Remove(chapter);
    }

    public void Remove(IList<Chapter> chapters)
    {
        _context.Chapter.RemoveRange(chapters);
    }

    public async Task<IEnumerable<Chapter>> GetChaptersByIdsAsync(IList<int> chapterIds, ChapterIncludes includes = ChapterIncludes.None)
    {
        return await _context.Chapter
            .Where(c => chapterIds.Contains(c.Id))
            .Includes(includes)
            .AsSplitQuery()
            .ToListAsync();
    }

    /// <summary>
    /// Populates a partial IChapterInfoDto
    /// </summary>
    /// <returns></returns>
    public async Task<IChapterInfoDto?> GetChapterInfoDtoAsync(int chapterId)
    {
        var chapterInfo = await _context.Chapter
            .Where(c => c.Id == chapterId)
            .Join(_context.Volume, c => c.VolumeId, v => v.Id, (chapter, volume) => new
            {
                ChapterNumber = chapter.MinNumber,
                VolumeNumber = volume.Name,
                VolumeId = volume.Id,
                chapter.IsSpecial,
                chapter.TitleName,
                volume.SeriesId,
                chapter.Pages,
            })
            .Join(_context.Series, data => data.SeriesId, series => series.Id, (data, series) => new
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
            .SingleOrDefaultAsync();

        return chapterInfo;
    }

    public Task<int> GetChapterTotalPagesAsync(int chapterId)
    {
        return _context.Chapter
            .Where(c => c.Id == chapterId)
            .Select(c => c.Pages)
            .FirstOrDefaultAsync();
    }
    public async Task<ChapterDto?> GetChapterDtoAsync(int chapterId, int userId)
    {
        var chapter = await _context.Chapter
            .Includes(ChapterIncludes.Files | ChapterIncludes.People)
            .ProjectToWithProgress<Chapter, ChapterDto>(_mapper, userId)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == chapterId);

        return chapter;
    }

    public async Task<IList<ChapterDto>> GetChapterDtoByIdsAsync(IEnumerable<int> chapterIds, int userId)
    {
        var chapters = await _context.Chapter
                .Where(c => chapterIds.Contains(c.Id))
                .Includes(ChapterIncludes.Files | ChapterIncludes.People)
                .ProjectToWithProgress<Chapter, ChapterDto>(_mapper, userId)
                .AsSplitQuery()
                .ToListAsync() ;

        return chapters;
    }

    public async Task<ChapterMetadataDto?> GetChapterMetadataDtoAsync(int chapterId, ChapterIncludes includes = ChapterIncludes.Files)
    {
        var chapter = await _context.Chapter
            .Includes(includes)
            .ProjectTo<ChapterMetadataDto>(_mapper.ConfigurationProvider)
            .AsNoTracking()
            .AsSplitQuery()
            .SingleOrDefaultAsync(c => c.Id == chapterId);

        return chapter;
    }

    /// <summary>
    /// Returns non-tracked files for a given chapterId
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    public async Task<IList<MangaFile>> GetFilesForChapterAsync(int chapterId)
    {
        return await _context.MangaFile
            .Where(c => chapterId == c.ChapterId)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Returns a Chapter for an Id. Includes linked <see cref="MangaFile"/>s.
    /// </summary>
    /// <param name="chapterId"></param>
    /// <param name="includes"></param>
    /// <returns></returns>
    public async Task<Chapter?> GetChapterAsync(int chapterId, ChapterIncludes includes = ChapterIncludes.Files)
    {
        return await _context.Chapter
            .Includes(includes)
            .OrderBy(c => c.SortOrder)
            .FirstOrDefaultAsync(c => c.Id == chapterId);
    }

    /// <summary>
    /// Returns Chapters for a volume id.
    /// </summary>
    /// <param name="volumeId"></param>
    /// <returns></returns>
    public async Task<IList<Chapter>> GetChaptersAsync(int volumeId, ChapterIncludes includes = ChapterIncludes.None)
    {
        return await _context.Chapter
            .Where(c => c.VolumeId == volumeId)
            .Includes(includes)
            .OrderBy(c => c.SortOrder)
            .ToListAsync();
    }

    /// <summary>
    /// Returns Chapters for a volume id with Progress
    /// </summary>
    /// <param name="volumeId"></param>
    /// <returns></returns>
    public async Task<IList<ChapterDto>> GetChapterDtosAsync(int volumeId, int userId)
    {
        return await _context.Chapter
            .Where(c => c.VolumeId == volumeId)
            .Includes(ChapterIncludes.Files | ChapterIncludes.People)
            .OrderBy(c => c.SortOrder)
            .ProjectToWithProgress<Chapter, ChapterDto>(_mapper, userId)
            .ToListAsync();
    }

    /// <summary>
    /// Returns the cover image for a chapter id.
    /// </summary>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    public async Task<string?> GetChapterCoverImageAsync(int chapterId)
    {
        return await _context.Chapter
            .Where(c => c.Id == chapterId)
            .Select(c => c.CoverImage)
            .SingleOrDefaultAsync();
    }

    public async Task<IList<string>> GetAllCoverImagesAsync()
    {
        return (await _context.Chapter
            .Select(c => c.CoverImage)
            .Where(t => !string.IsNullOrEmpty(t))
            .ToListAsync())!;
    }

    public async Task<IList<Chapter>> GetAllChaptersWithCoversInDifferentEncoding(EncodeFormat format)
    {
        var extension = format.GetExtension();
        return await _context.Chapter
            .Where(c => !string.IsNullOrEmpty(c.CoverImage)  && !c.CoverImage.EndsWith(extension))
            .ToListAsync();
    }

    /// <summary>
    /// Returns cover images for locked chapters
    /// </summary>
    /// <returns></returns>
    public async Task<IEnumerable<string>> GetCoverImagesForLockedChaptersAsync()
    {
        return (await _context.Chapter
            .Where(c => c.CoverImageLocked)
            .Select(c => c.CoverImage)
            .Where(t => !string.IsNullOrEmpty(t))
            .ToListAsync())!;
    }

    /// <summary>
    /// Returns non-tracked files for a set of <paramref name="chapterIds"/>
    /// </summary>
    /// <param name="chapterIds">List of chapter Ids</param>
    /// <returns></returns>
    public async Task<IList<MangaFile>> GetFilesForChaptersAsync(IReadOnlyList<int> chapterIds)
    {
        return await _context.MangaFile
            .Where(c => chapterIds.Contains(c.ChapterId))
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Includes Volumes
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    public IQueryable<Chapter> GetChaptersForSeries(int seriesId)
    {
        return _context.Chapter
            .Where(c => c.Volume.SeriesId == seriesId)
            .OrderBy(c => c.SortOrder)
            .Include(c => c.Volume);
    }

    public async Task<IList<Chapter>> GetAllChaptersForSeries(int seriesId)
    {
        return await _context.Chapter
            .Where(c => c.Volume.SeriesId == seriesId)
            .OrderBy(c => c.SortOrder)
            .Include(c => c.Volume)
            .Include(c => c.People)
            .ThenInclude(cp => cp.Person)
            .ToListAsync();
    }

    public async Task<int> GetAverageUserRating(int chapterId, int userId)
    {
        // If there is 0 or 1 rating and that rating is you, return 0 back
        var countOfRatingsThatAreUser = await _context.AppUserChapterRating
            .Where(r => r.ChapterId == chapterId && r.HasBeenRated)
            .CountAsync(u => u.AppUserId == userId);
        if (countOfRatingsThatAreUser == 1)
        {
            return 0;
        }
        var avg = (await _context.AppUserChapterRating
            .Where(r => r.ChapterId == chapterId && r.HasBeenRated)
            .AverageAsync(r => (int?) r.Rating));
        return avg.HasValue ? (int) (avg.Value * 20) : 0;
    }

    public async Task<IList<UserReviewDto>> GetExternalChapterReviewDtos(int chapterId)
    {
        return await _context.Chapter
            .Where(c => c.Id == chapterId)
            .SelectMany(c => c.ExternalReviews)
            // Don't use ProjectTo, it fails to map int to float (??)
            .Select(r => _mapper.Map<UserReviewDto>(r))
            .ToListAsync();
    }

    public async Task<IList<ExternalReview>> GetExternalChapterReview(int chapterId)
    {
        return await _context.Chapter
            .Where(c => c.Id == chapterId)
            .SelectMany(c => c.ExternalReviews)
            .ToListAsync();
    }

    public async Task<IList<RatingDto>> GetExternalChapterRatingDtos(int chapterId)
    {
        return await _context.Chapter
            .Where(c => c.Id == chapterId)
            .SelectMany(c => c.ExternalRatings)
            .ProjectTo<RatingDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<IList<ExternalRating>> GetExternalChapterRatings(int chapterId)
    {
        return await _context.Chapter
            .Where(c => c.Id == chapterId)
            .SelectMany(c => c.ExternalRatings)
            .ToListAsync();
    }

    public async Task<ChapterDto?> GetCurrentlyReadingChapterAsync(int seriesId, int userId)
    {
        var chapterWithProgress = await _context.AppUserProgresses
            .Where(p => p.AppUserId == userId)
            .Join(
                _context.Chapter
                    .Include(c => c.Volume)
                    .Include(c => c.Files),
                p => p.ChapterId,
                c => c.Id,
                (p, c) => new { Chapter = c, p.PagesRead }
            )
            .Where(x => x.Chapter.Volume.SeriesId == seriesId)
            .Where(x => x.Chapter.Volume.Number != Parser.LooseLeafVolumeNumber)
            .Where(x => x.PagesRead > 0 && x.PagesRead < x.Chapter.Pages)
            .OrderBy(x => x.Chapter.Volume.Number)
            .ThenBy(x => x.Chapter.SortOrder)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (chapterWithProgress == null) return null;

        // Map chapter to DTO
        var dto = _mapper.Map<ChapterDto>(chapterWithProgress.Chapter);
        dto.PagesRead = chapterWithProgress.PagesRead;

        return dto;
    }

    public async Task<ChapterDto?> GetFirstChapterForSeriesAsync(int seriesId, int userId)
    {
        // Get the chapter entity with proper ordering
        return await _context.Chapter
            .Include(c => c.Volume)
            .Include(c => c.Files)
            .Where(c => c.Volume.SeriesId == seriesId)
            .ApplyDefaultChapterOrdering()
            .AsNoTracking()
            .ProjectToWithProgress<Chapter, ChapterDto>(_mapper, userId)
            .FirstOrDefaultAsync();
    }

    public async Task<ChapterDto?> GetFirstChapterForVolumeAsync(int volumeId, int userId)
    {
        // Get the chapter entity with proper ordering
        return await _context.Chapter
            .Include(c => c.Volume)
            .Include(c => c.Files)
            .Where(c => c.Volume.Id == volumeId)
            .ApplyDefaultChapterOrdering()
            .AsNoTracking()
            .ProjectToWithProgress<Chapter, ChapterDto>(_mapper, userId)
            .FirstOrDefaultAsync();
    }

    public async Task<IList<ChapterDto>> GetChapterDtosAsync(IEnumerable<int> chapterIds, int userId)
    {
        var chapterIdList = chapterIds.ToList();
        if (chapterIdList.Count == 0) return [];

        return await _context.Chapter
            .Where(c => chapterIdList.Contains(c.Id))
            .ProjectToWithProgress<Chapter, ChapterDto>(_mapper, userId)
            .ToListAsync();
    }
}
