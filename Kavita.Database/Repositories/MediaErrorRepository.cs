using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Kavita.API.Repositories;
using Kavita.Common.Extensions;
using Kavita.Models.DTOs.MediaErrors;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Parser;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Repositories;

public class MediaErrorRepository(DataContext context, IMapper mapper) : IMediaErrorRepository
{
    public void Attach(MediaError? error)
    {
        if (error == null) return;
        context.MediaError.Attach(error);
    }

    public void Remove(IList<MediaError> errors)
    {
        context.MediaError.RemoveRange(errors);
    }

    public async Task<IEnumerable<MediaErrorDto>> GetAllErrorDtosAsync(CancellationToken ct = default)
    {
        return await context.MediaError
            .OrderByDescending(m => m.LastSeenUtc)
            .ProjectTo<MediaErrorDto>(mapper.ConfigurationProvider)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public Task<bool> ExistsAsync(MediaError error, CancellationToken ct = default)
    {
        return context.MediaError.AnyAsync(m => m.FilePath.Equals(error.FilePath)
                                                 && m.Comment.Equals(error.Comment)
                                                 && m.Details.Equals(error.Details), ct
        );
    }

    public async Task DeleteAll(CancellationToken ct = default)
    {
        await context.MediaError.ExecuteDeleteAsync(ct);
    }

    public Task<MediaError?> GetProducerErrorAsync(string filePath, MediaErrorReason reason, CancellationToken ct = default)
    {
        return context.MediaError
            .FirstOrDefaultAsync(m => m.Producer != MediaErrorProducer.Scanner && m.FilePath == filePath && m.Reason == reason, ct);
    }

    public Task<List<MediaError>> GetProducerErrorsAsync(int libraryId, CancellationToken ct = default)
    {
        return context.MediaError
            .Where(m => m.LibraryId == libraryId && m.Producer != MediaErrorProducer.Scanner)
            .ToListAsync(ct);
    }

    public async Task<MediaErrorOwner> GetOwnerAsync(string filePath, CancellationToken ct = default)
    {
        var owner = await context.MangaFile
            .Where(f => f.FilePath == filePath)
            .Select(f => new MediaErrorOwner(f.Chapter.Volume.Series.LibraryId, f.Chapter.Volume.SeriesId))
            .FirstOrDefaultAsync(ct);
        if (owner != null) return owner;

        var folders = await context.FolderPath
            .Select(f => new { f.Path, f.LibraryId })
            .ToListAsync(ct);
        var libraryId = folders
            .Where(f => filePath.IsInsideFolder(f.Path))
            .OrderByDescending(f => f.Path.Length)
            .Select(f => (int?) f.LibraryId)
            .FirstOrDefault();

        return new MediaErrorOwner(libraryId, null);
    }

    public Task<List<FailedFile>> GetFailedFilesAsync(int libraryId, CancellationToken ct = default)
    {
        return context.MediaError
            .Where(m => m.LibraryId == libraryId && m.Producer == MediaErrorProducer.Scanner
                        && m.Bytes != null && m.FileLastWriteTimeUtc != null)
            .ProjectTo<FailedFile>(mapper.ConfigurationProvider)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public Task<List<MediaError>> GetScannerErrorsAsync(int libraryId, IList<string> filePaths, CancellationToken ct = default)
    {
        return context.MediaError
            .Where(m => m.LibraryId == libraryId && m.Producer == MediaErrorProducer.Scanner && filePaths.Contains(m.FilePath))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Finds the series id by looking at neighboring files and attach to the Scan Issues. If <see cref="MediaErrorReasons.Imported"/>, then will do a direct lookup instead.
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="filePaths"></param>
    /// <param name="filesByFolder"></param>
    /// <param name="ct"></param>
    public async Task AssignScannerErrorsToSeriesAsync(int libraryId, IList<string> filePaths,
        IReadOnlyDictionary<string, IList<string>> filesByFolder, CancellationToken ct = default)
    {
        if (filePaths.Count == 0) return;

        var unassigned = await context.MediaError
            .Where(m => m.LibraryId == libraryId && m.Producer == MediaErrorProducer.Scanner && m.SeriesId == null
                        && filePaths.Contains(m.FilePath))
            .ToListAsync(ct);
        if (unassigned.Count == 0) return;

        // Most rows are files that failed to parse and have no MangaFile, so the other files in their folder are looked up too
        var paths = unassigned
            .Select(m => m.FilePath.FolderOf())
            .Distinct()
            .SelectMany(folder => filesByFolder.GetValueOrDefault(folder) ?? [])
            .Concat(unassigned.Select(m => m.FilePath))
            .Distinct()
            .ToList();

        var seriesByFile = await context.MangaFile
            .Where(f => f.Chapter.Volume.Series.LibraryId == libraryId && paths.Contains(f.FilePath))
            .Select(f => new { f.FilePath, f.Chapter.Volume.SeriesId })
            .ToListAsync(ct);
        var seriesByPath = seriesByFile
            .GroupBy(f => f.FilePath)
            .ToDictionary(g => g.Key, g => g.First().SeriesId);
        var seriesByFolder = seriesByFile.ToLookup(f => f.FilePath.FolderOf(), f => f.SeriesId);

        foreach (var row in unassigned)
        {
            if (seriesByPath.TryGetValue(row.FilePath, out var ownSeriesId))
            {
                row.SeriesId = ownSeriesId;
                continue;
            }

            var seriesIds = seriesByFolder[row.FilePath.FolderOf()].Distinct().ToList();
            if (seriesIds.Count == 1) row.SeriesId = seriesIds[0];
        }
    }

    public Task<int> GetUnreadableFileCountAsync(int libraryId, CancellationToken ct = default)
    {
        return GetUnreadableFiles(libraryId).CountAsync(ct);
    }

    public Task<List<ScanIssueSummaryItemDto>> GetUnreadableFilesAsync(int libraryId, int take, CancellationToken ct = default)
    {
        return GetUnreadableFiles(libraryId)
            .OrderByDescending(m => m.LastSeenUtc)
            .Take(take)
            .ProjectTo<ScanIssueSummaryItemDto>(mapper.ConfigurationProvider)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    private IQueryable<MediaError> GetUnreadableFiles(int libraryId)
    {
        return context.MediaError
            .Where(m => m.LibraryId == libraryId && m.Producer == MediaErrorProducer.Scanner && !m.IsDismissed
                        && !MediaErrorReasons.Imported.Contains(m.Reason));
    }
}
