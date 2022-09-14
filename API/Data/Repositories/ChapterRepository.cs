using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.DTOs.Metadata;
using API.DTOs.Reader;
using API.Entities;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface IChapterRepository
{
    void Update(Chapter chapter);
    Task<IEnumerable<Chapter>> GetChaptersByIdsAsync(IList<int> chapterIds);
    Task<IChapterInfoDto> GetChapterInfoDtoAsync(int chapterId);
    Task<int> GetChapterTotalPagesAsync(int chapterId);
    Task<Chapter> GetChapterAsync(int chapterId);
    Task<ChapterDto> GetChapterDtoAsync(int chapterId);
    Task<ChapterMetadataDto> GetChapterMetadataDtoAsync(int chapterId);
    Task<IList<MangaFile>> GetFilesForChapterAsync(int chapterId);
    Task<IList<Chapter>> GetChaptersAsync(int volumeId);
    Task<IList<MangaFile>> GetFilesForChaptersAsync(IReadOnlyList<int> chapterIds);
    Task<string> GetChapterCoverImageAsync(int chapterId);
    Task<IList<string>> GetAllCoverImagesAsync();
    Task<IEnumerable<string>> GetCoverImagesForLockedChaptersAsync();
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

    public async Task<IEnumerable<Chapter>> GetChaptersByIdsAsync(IList<int> chapterIds)
    {
        return await _context.Chapter
            .Where(c => chapterIds.Contains(c.Id))
            .Include(c => c.Volume)
            .AsSplitQuery()
            .ToListAsync();
    }

    /// <summary>
    /// Populates a partial IChapterInfoDto
    /// </summary>
    /// <returns></returns>
    public async Task<IChapterInfoDto> GetChapterInfoDtoAsync(int chapterId)
    {
        var chapterInfo = await _context.Chapter
            .Where(c => c.Id == chapterId)
            .Join(_context.Volume, c => c.VolumeId, v => v.Id, (chapter, volume) => new
            {
                ChapterNumber = chapter.Range,
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
                ChapterNumber = data.ChapterNumber,
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
            .SingleOrDefaultAsync();
    }
    public async Task<ChapterDto> GetChapterDtoAsync(int chapterId)
    {
        var chapter = await _context.Chapter
            .Include(c => c.Files)
            .ProjectTo<ChapterDto>(_mapper.ConfigurationProvider)
            .AsNoTracking()
            .AsSplitQuery()
            .SingleOrDefaultAsync(c => c.Id == chapterId);

        return chapter;
    }

    public async Task<ChapterMetadataDto> GetChapterMetadataDtoAsync(int chapterId)
    {
        var chapter = await _context.Chapter
            .Include(c => c.Files)
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
    /// <returns></returns>
    public async Task<Chapter> GetChapterAsync(int chapterId)
    {
        return await _context.Chapter
            .Include(c => c.Files)
            .AsSplitQuery()
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
    public async Task<string> GetChapterCoverImageAsync(int chapterId)
    {

        return await _context.Chapter
            .Where(c => c.Id == chapterId)
            .Select(c => c.CoverImage)
            .AsNoTracking()
            .SingleOrDefaultAsync();
    }

    public async Task<IList<string>> GetAllCoverImagesAsync()
    {
        return await _context.Chapter
            .Select(c => c.CoverImage)
            .Where(t => !string.IsNullOrEmpty(t))
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Returns cover images for locked chapters
    /// </summary>
    /// <returns></returns>
    public async Task<IEnumerable<string>> GetCoverImagesForLockedChaptersAsync()
    {
        return await _context.Chapter
            .Where(c => c.CoverImageLocked)
            .Select(c => c.CoverImage)
            .Where(t => !string.IsNullOrEmpty(t))
            .AsNoTracking()
            .ToListAsync();
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
}
