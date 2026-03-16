using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Kavita.API.Repositories;
using Kavita.Database.Extensions;
using Kavita.Models.DTOs;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Repositories;

public class VolumeRepository(DataContext context, IMapper mapper) : IVolumeRepository
{
    public void Add(Volume volume)
    {
        context.Volume.Add(volume);
    }

    public void Update(Volume volume)
    {
        context.Entry(volume).State = EntityState.Modified;
    }

    public void Remove(Volume volume)
    {
        context.Volume.Remove(volume);
    }
    public void Remove(IList<Volume> volumes)
    {
        context.Volume.RemoveRange(volumes);
    }

    /// <summary>
    /// Returns a list of non-tracked files for a given volume.
    /// </summary>
    /// <param name="volumeId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<IList<MangaFile>> GetFilesForVolume(int volumeId, CancellationToken ct = default)
    {
        return await context.Chapter
            .Where(c => volumeId == c.VolumeId)
            .Include(c => c.Files)
            .SelectMany(c => c.Files)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(ct);
    }

    /// <summary>
    /// Returns the cover image file for the given volume
    /// </summary>
    /// <param name="volumeId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<string?> GetVolumeCoverImageAsync(int volumeId, CancellationToken ct = default)
    {
        return await context.Volume
            .Where(v => v.Id == volumeId)
            .Select(v => v.CoverImage)
            .SingleOrDefaultAsync(ct);
    }

    /// <summary>
    /// Returns all chapter Ids belonging to a list of Volume Ids
    /// </summary>
    /// <param name="volumeIds"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<IList<int>> GetChapterIdsByVolumeIds(IReadOnlyList<int> volumeIds, CancellationToken ct = default)
    {
        return await context.Chapter
            .Where(c => volumeIds.Contains(c.VolumeId))
            .Select(c => c.Id)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Returns all volumes that contain a seriesId in a passed array.
    /// </summary>
    /// <param name="seriesIds"></param>
    /// <param name="includeChapters">Include chapter entities</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<IEnumerable<Volume>> GetVolumesForSeriesAsync(IList<int> seriesIds, bool includeChapters = false,
        CancellationToken ct = default)
    {
        var query = context.Volume
            .Where(v => seriesIds.Contains(v.SeriesId));

        if (includeChapters)
        {
            query = query
                .Includes(VolumeIncludes.Chapters)
                .AsSplitQuery();
        }
        var volumes =  await query.ToListAsync(ct);

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
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<VolumeDto?> GetVolumeDtoAsync(int volumeId, int userId, CancellationToken ct = default)
    {
        return await context.Volume
            .Where(vol => vol.Id == volumeId)
            .Includes(VolumeIncludes.Chapters | VolumeIncludes.Files)
            .AsSplitQuery()
            .OrderBy(v => v.MinNumber)
            .ProjectToWithProgress<Volume, VolumeDto>(mapper, userId)
            .FirstOrDefaultAsync(vol => vol.Id == volumeId, ct);
    }

    /// <summary>
    /// Returns the full Volumes including Chapters and Files for a given series
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<IEnumerable<Volume>> GetVolumes(int seriesId, CancellationToken ct = default)
    {
        return await context.Volume
            .Where(vol => vol.SeriesId == seriesId)
            .Includes(VolumeIncludes.Chapters | VolumeIncludes.Files)
            .AsSplitQuery()
            .OrderBy(vol => vol.MinNumber)
            .ToListAsync(ct);
    }
    public async Task<IList<Volume>> GetVolumesById(IList<int> volumeIds, VolumeIncludes includes = VolumeIncludes.None,
        CancellationToken ct = default)
    {
        return await context.Volume
            .Where(vol => volumeIds.Contains(vol.Id))
            .Includes(includes)
            .AsSplitQuery()
            .OrderBy(vol => vol.MinNumber)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Returns a single volume with Chapter and Files
    /// </summary>
    /// <param name="volumeId"></param>
    /// <param name="includes"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<Volume?> GetVolumeByIdAsync(int volumeId, VolumeIncludes includes = VolumeIncludes.Files,
        CancellationToken ct = default)
    {
        return await context.Volume
            .Includes(includes)
            .AsSplitQuery()
            .SingleOrDefaultAsync(vol => vol.Id == volumeId, ct);
    }


    /// <summary>
    /// Returns all volumes for a given series with progress information attached. Includes all Chapters as well.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="userId"></param>
    /// <param name="includes"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<IList<VolumeDto>> GetVolumesDtoAsync(int seriesId, int userId,
        VolumeIncludes includes = VolumeIncludes.Chapters, CancellationToken ct = default)
    {
        return await context.Volume
            .Where(vol => vol.SeriesId == seriesId)
            .Includes(includes)
            .OrderBy(volume => volume.MinNumber)
            .ProjectToWithProgress<Volume, VolumeDto>(mapper, userId)
            .AsSplitQuery()
            .ToListAsync(ct);
    }

    public async Task<IList<Volume>> GetAllWithCoversInDifferentEncoding(EncodeFormat encodeFormat,
        CancellationToken ct = default)
    {
        var extension = encodeFormat.GetExtension();
        return await context.Volume
            .Includes(VolumeIncludes.Chapters)
            .Where(c => !string.IsNullOrEmpty(c.CoverImage) && !c.CoverImage.EndsWith(extension))
            .AsSplitQuery()
            .ToListAsync(ct);
    }


    /// <summary>
    /// Returns cover images for locked chapters
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<IEnumerable<string>> GetCoverImagesForLockedVolumesAsync(CancellationToken ct = default)
    {
        return (await context.Volume
            .Where(c => c.CoverImageLocked)
            .Select(c => c.CoverImage)
            .Where(t => !string.IsNullOrEmpty(t))
            .ToListAsync(ct))!;
    }

    public async Task<long> GetFilesizeAsync(int volumeId, CancellationToken ct = default)
    {
        return await context.Chapter
            .Where(c => volumeId == c.VolumeId)
            .Include(c => c.Files)
            .SelectMany(c => c.Files)
            .SumAsync(f => f.Bytes, cancellationToken: ct);
    }

    public async Task<Dictionary<int, long>> GetFilesizesAsync(IList<int> volumeIds, CancellationToken ct = default)
    {
        return await volumeIds.BatchToDictionaryAsync(50, batch =>
            context.Chapter
                .Where(c => batch.Contains(c.VolumeId))
                .GroupBy(c => c.VolumeId)
                .Select(g => new
                {
                    VolumeId = g.Key,
                    TotalBytes = g.SelectMany(c => c.Files).Sum(f => f.Bytes)
                })
                .ToDictionaryAsync(x => x.VolumeId, x => x.TotalBytes, cancellationToken: ct));
    }
}
