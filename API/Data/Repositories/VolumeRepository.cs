using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Services;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Kavita.Common;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface IVolumeRepository
{
    void Add(Volume volume);
    void Update(Volume volume);
    void Remove(Volume volume);
    Task<IList<MangaFile>> GetFilesForVolume(int volumeId);
    Task<string?> GetVolumeCoverImageAsync(int volumeId);
    Task<IList<int>> GetChapterIdsByVolumeIds(IReadOnlyList<int> volumeIds);
    Task<IEnumerable<VolumeDto>> GetVolumesDtoAsync(int seriesId, int userId);
    Task<Volume?> GetVolumeAsync(int volumeId);
    Task<VolumeDto?> GetVolumeDtoAsync(int volumeId, int userId);
    Task<IEnumerable<Volume>> GetVolumesForSeriesAsync(IList<int> seriesIds, bool includeChapters = false);
    Task<IEnumerable<Volume>> GetVolumes(int seriesId);
    Task<Volume?> GetVolumeByIdAsync(int volumeId);
    Task<IList<Volume>> GetAllWithCoversInDifferentEncoding(EncodeFormat encodeFormat);
}
public class VolumeRepository : IVolumeRepository
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;

    public VolumeRepository(DataContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public void Add(Volume volume)
    {
        _context.Volume.Add(volume);
    }

    public void Update(Volume volume)
    {
        _context.Entry(volume).State = EntityState.Modified;
    }

    public void Remove(Volume volume)
    {
        _context.Volume.Remove(volume);
    }

    /// <summary>
    /// Returns a list of non-tracked files for a given volume.
    /// </summary>
    /// <param name="volumeId"></param>
    /// <returns></returns>
    public async Task<IList<MangaFile>> GetFilesForVolume(int volumeId)
    {
        return await _context.Chapter
            .Where(c => volumeId == c.VolumeId)
            .Include(c => c.Files)
            .SelectMany(c => c.Files)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Returns the cover image file for the given volume
    /// </summary>
    /// <param name="volumeId"></param>
    /// <returns></returns>
    public async Task<string?> GetVolumeCoverImageAsync(int volumeId)
    {
        return await _context.Volume
            .Where(v => v.Id == volumeId)
            .Select(v => v.CoverImage)
            .SingleOrDefaultAsync();
    }

    /// <summary>
    /// Returns all chapter Ids belonging to a list of Volume Ids
    /// </summary>
    /// <param name="volumeIds"></param>
    /// <returns></returns>
    public async Task<IList<int>> GetChapterIdsByVolumeIds(IReadOnlyList<int> volumeIds)
    {
        return await _context.Chapter
            .Where(c => volumeIds.Contains(c.VolumeId))
            .Select(c => c.Id)
            .ToListAsync();
    }

    /// <summary>
    /// Returns all volumes that contain a seriesId in passed array.
    /// </summary>
    /// <param name="seriesIds"></param>
    /// <param name="includeChapters">Include chapter entities</param>
    /// <returns></returns>
    public async Task<IEnumerable<Volume>> GetVolumesForSeriesAsync(IList<int> seriesIds, bool includeChapters = false)
    {
        var query = _context.Volume
            .Where(v => seriesIds.Contains(v.SeriesId));

        if (includeChapters)
        {
            query = query.Include(v => v.Chapters).AsSplitQuery();
        }
        return await query.ToListAsync();
    }

    /// <summary>
    /// Returns an individual Volume including Chapters and Files and Reading Progress for a given volumeId
    /// </summary>
    /// <param name="volumeId"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<VolumeDto?> GetVolumeDtoAsync(int volumeId, int userId)
    {
        var volume = await _context.Volume
            .Where(vol => vol.Id == volumeId)
            .Include(vol => vol.Chapters)
            .ThenInclude(c => c.Files)
            .AsSplitQuery()
            .ProjectTo<VolumeDto>(_mapper.ConfigurationProvider)
            .SingleOrDefaultAsync(vol => vol.Id == volumeId);

        if (volume == null) return null;

        var volumeList = new List<VolumeDto>() {volume};
        await AddVolumeModifiers(userId, volumeList);

        return volumeList[0];
    }

    /// <summary>
    /// Returns the full Volumes including Chapters and Files for a given series
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    public async Task<IEnumerable<Volume>> GetVolumes(int seriesId)
    {
        return await _context.Volume
            .Where(vol => vol.SeriesId == seriesId)
            .Include(vol => vol.Chapters)
            .ThenInclude(c => c.Files)
            .AsSplitQuery()
            .OrderBy(vol => vol.Number)
            .ToListAsync();
    }

    /// <summary>
    /// Returns a single volume with Chapter and Files
    /// </summary>
    /// <param name="volumeId"></param>
    /// <returns></returns>
    public async Task<Volume?> GetVolumeAsync(int volumeId)
    {
        return await _context.Volume
            .Include(vol => vol.Chapters)
            .ThenInclude(c => c.Files)
            .AsSplitQuery()
            .SingleOrDefaultAsync(vol => vol.Id == volumeId);
    }


    /// <summary>
    /// Returns all volumes for a given series with progress information attached. Includes all Chapters as well.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<IEnumerable<VolumeDto>> GetVolumesDtoAsync(int seriesId, int userId)
    {
        var volumes =  await _context.Volume
            .Where(vol => vol.SeriesId == seriesId)
            .Include(vol => vol.Chapters)
            .ThenInclude(c => c.People)
            .Include(vol => vol.Chapters)
            .ThenInclude(c => c.Tags)
            .OrderBy(volume => volume.Number)
            .ProjectTo<VolumeDto>(_mapper.ConfigurationProvider)
            .AsNoTracking()
            .AsSplitQuery()
            .ToListAsync();

        await AddVolumeModifiers(userId, volumes);
        SortSpecialChapters(volumes);

        return volumes;
    }

    public async Task<Volume?> GetVolumeByIdAsync(int volumeId)
    {
        return await _context.Volume.SingleOrDefaultAsync(x => x.Id == volumeId);
    }

    public async Task<IList<Volume>> GetAllWithCoversInDifferentEncoding(EncodeFormat encodeFormat)
    {
        var extension = encodeFormat.GetExtension();
        return await _context.Volume
                    .Where(c => !string.IsNullOrEmpty(c.CoverImage) && !c.CoverImage.EndsWith(extension))
                    .ToListAsync();
    }


    private static void SortSpecialChapters(IEnumerable<VolumeDto> volumes)
    {
        foreach (var v in volumes.Where(vDto => vDto.Number == 0))
        {
            v.Chapters = v.Chapters.OrderByNatural(x => x.Range).ToList();
        }
    }


    private async Task AddVolumeModifiers(int userId, IReadOnlyCollection<VolumeDto> volumes)
    {
        var volIds = volumes.Select(s => s.Id);
        var userProgress = await _context.AppUserProgresses
            .Where(p => p.AppUserId == userId && volIds.Contains(p.VolumeId))
            .AsNoTracking()
            .ToListAsync();

        foreach (var v in volumes)
        {
            foreach (var c in v.Chapters)
            {
                var progresses = userProgress.Where(p => p.ChapterId == c.Id).ToList();
                if (progresses.Count == 0) continue;
                c.PagesRead = progresses.Sum(p => p.PagesRead);
                c.LastReadingProgressUtc = progresses.Max(p => p.LastModifiedUtc);
            }

            v.PagesRead = userProgress.Where(p => p.VolumeId == v.Id).Sum(p => p.PagesRead);
        }
    }
}
