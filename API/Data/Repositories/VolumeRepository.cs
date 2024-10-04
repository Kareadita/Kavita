using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Extensions.QueryExtensions;
using API.Services;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Kavita.Common;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

[Flags]
public enum VolumeIncludes
{
    None = 1,
    Chapters = 2,
    People = 4,
    Tags = 8,
    /// <summary>
    /// This will include Chapters by default
    /// </summary>
    Files = 16
}

public interface IVolumeRepository
{
    void Add(Volume volume);
    void Update(Volume volume);
    void Remove(Volume volume);
    Task<IList<MangaFile>> GetFilesForVolume(int volumeId);
    Task<string?> GetVolumeCoverImageAsync(int volumeId);
    Task<IList<int>> GetChapterIdsByVolumeIds(IReadOnlyList<int> volumeIds);
    Task<IList<VolumeDto>> GetVolumesDtoAsync(int seriesId, int userId, VolumeIncludes includes = VolumeIncludes.Chapters);
    Task<Volume?> GetVolumeAsync(int volumeId, VolumeIncludes includes = VolumeIncludes.Files);
    Task<VolumeDto?> GetVolumeDtoAsync(int volumeId, int userId);
    Task<IEnumerable<Volume>> GetVolumesForSeriesAsync(IList<int> seriesIds, bool includeChapters = false);
    Task<IEnumerable<Volume>> GetVolumes(int seriesId);
    Task<Volume?> GetVolumeByIdAsync(int volumeId);
    Task<IList<Volume>> GetAllWithCoversInDifferentEncoding(EncodeFormat encodeFormat);
    Task<IEnumerable<string>> GetCoverImagesForLockedVolumesAsync();
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
            query = query
                .Includes(VolumeIncludes.Chapters)
                .AsSplitQuery();
        }
        var volumes =  await query.ToListAsync();

        foreach (var volume in volumes)
        {
            volume.Chapters = volume.Chapters.OrderBy(c => c.SortOrder).ToList();
        }

        return volumes;
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
            .Includes(VolumeIncludes.Chapters | VolumeIncludes.Files)
            .AsSplitQuery()
            .OrderBy(v => v.MinNumber)
            .ProjectTo<VolumeDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(vol => vol.Id == volumeId);

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
            .Includes(VolumeIncludes.Chapters | VolumeIncludes.Files)
            .AsSplitQuery()
            .OrderBy(vol => vol.MinNumber)
            .ToListAsync();
    }

    /// <summary>
    /// Returns a single volume with Chapter and Files
    /// </summary>
    /// <param name="volumeId"></param>
    /// <returns></returns>
    public async Task<Volume?> GetVolumeAsync(int volumeId, VolumeIncludes includes = VolumeIncludes.Files)
    {
        return await _context.Volume
            .Includes(includes)
            .AsSplitQuery()
            .SingleOrDefaultAsync(vol => vol.Id == volumeId);
    }


    /// <summary>
    /// Returns all volumes for a given series with progress information attached. Includes all Chapters as well.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<IList<VolumeDto>> GetVolumesDtoAsync(int seriesId, int userId, VolumeIncludes includes = VolumeIncludes.Chapters)
    {
        var volumes =  await _context.Volume
            .Where(vol => vol.SeriesId == seriesId)
            .Includes(includes)
            .OrderBy(volume => volume.MinNumber)
            .ProjectTo<VolumeDto>(_mapper.ConfigurationProvider)
            .AsSplitQuery()
            .ToListAsync();

        await AddVolumeModifiers(userId, volumes);

        return volumes;
    }

    public async Task<Volume?> GetVolumeByIdAsync(int volumeId)
    {
        return await _context.Volume.FirstOrDefaultAsync(x => x.Id == volumeId);
    }

    public async Task<IList<Volume>> GetAllWithCoversInDifferentEncoding(EncodeFormat encodeFormat)
    {
        var extension = encodeFormat.GetExtension();
        return await _context.Volume
            .Includes(VolumeIncludes.Chapters)
            .Where(c => !string.IsNullOrEmpty(c.CoverImage) && !c.CoverImage.EndsWith(extension))
            .AsSplitQuery()
            .ToListAsync();
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
                c.LastReadingProgress = progresses.Max(p => p.LastModified);
            }

            v.PagesRead = userProgress
                .Where(p => p.VolumeId == v.Id)
                .Sum(p => p.PagesRead);
        }
    }

    /// <summary>
    /// Returns cover images for locked chapters
    /// </summary>
    /// <returns></returns>
    public async Task<IEnumerable<string>> GetCoverImagesForLockedVolumesAsync()
    {
        return (await _context.Volume
            .Where(c => c.CoverImageLocked)
            .Select(c => c.CoverImage)
            .Where(t => !string.IsNullOrEmpty(t))
            .ToListAsync())!;
    }
}
