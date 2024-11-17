using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.DTOs.Metadata;
using API.DTOs.Reader;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Extensions.QueryExtensions;
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
    Tags = 32
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
    Task<ChapterDto?> GetChapterDtoAsync(int chapterId, ChapterIncludes includes = ChapterIncludes.Files);
    Task<ChapterMetadataDto?> GetChapterMetadataDtoAsync(int chapterId, ChapterIncludes includes = ChapterIncludes.Files);
    Task<IList<MangaFile>> GetFilesForChapterAsync(int chapterId);
    Task<IList<Chapter>> GetChaptersAsync(int volumeId, ChapterIncludes includes = ChapterIncludes.None);
    Task<IList<MangaFile>> GetFilesForChaptersAsync(IReadOnlyList<int> chapterIds);
    Task<string?> GetChapterCoverImageAsync(int chapterId);
    Task<IList<string>> GetAllCoverImagesAsync();
    Task<IList<Chapter>> GetAllChaptersWithCoversInDifferentEncoding(EncodeFormat format);
    Task<IEnumerable<string>> GetCoverImagesForLockedChaptersAsync();
    Task<ChapterDto> AddChapterModifiers(int userId, ChapterDto chapter);
    IEnumerable<Chapter> GetChaptersForSeries(int seriesId);
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
                ChapterNumber = data.ChapterNumber + string.Empty, // TODO: Fix this
                VolumeNumber = data.VolumeNumber + string.Empty, // TODO: Fix this
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
    public async Task<ChapterDto?> GetChapterDtoAsync(int chapterId, ChapterIncludes includes = ChapterIncludes.Files)
    {
        var chapter = await _context.Chapter
            .Includes(includes)
            .ProjectTo<ChapterDto>(_mapper.ConfigurationProvider)
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == chapterId);

        return chapter;
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

    public async Task<ChapterDto> AddChapterModifiers(int userId, ChapterDto chapter)
    {
        var progress = await _context.AppUserProgresses.Where(x =>
                x.AppUserId == userId && x.ChapterId == chapter.Id)
            .AsNoTracking()
            .FirstOrDefaultAsync();
        if (progress != null)
        {
            chapter.PagesRead = progress.PagesRead ;
            chapter.LastReadingProgressUtc = progress.LastModifiedUtc;
            chapter.LastReadingProgress = progress.LastModified;
        }
        else
        {
            chapter.PagesRead = 0;
            chapter.LastReadingProgressUtc = DateTime.MinValue;
            chapter.LastReadingProgress = DateTime.MinValue;
        }

        return chapter;
    }

    /// <summary>
    /// Includes Volumes
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    public IEnumerable<Chapter> GetChaptersForSeries(int seriesId)
    {
        return _context.Chapter
            .Where(c => c.Volume.SeriesId == seriesId)
            .OrderBy(c => c.SortOrder)
            .Include(c => c.Volume)
            .AsEnumerable();
    }
}
