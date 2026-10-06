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
            .OrderByDescending(m => m.Created)
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

    public Task<List<MediaError>> GetAllErrorsAsync(IList<string> comments, CancellationToken ct = default)
    {
        return context.MediaError
            .Where(m => comments.Contains(m.Comment))
            .ToListAsync(ct);
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

    public async Task AssignScannerErrorsToSeriesAsync(int libraryId, IList<string> filePaths, CancellationToken ct = default)
    {
        if (filePaths.Count == 0) return;

        var unassigned = await context.MediaError
            .Where(m => m.LibraryId == libraryId && m.Producer == MediaErrorProducer.Scanner && m.SeriesId == null
                        && filePaths.Contains(m.FilePath))
            .ToListAsync(ct);

        foreach (var folder in unassigned.GroupBy(m => m.FilePath.FolderOf()))
        {
            var prefix = folder.Key + "/";
            // LIKE treats _ as a wildcard, so we need the FolderOf check below
            var filesInFolder = await context.MangaFile
                .Where(f => f.Chapter.Volume.Series.LibraryId == libraryId && f.FilePath.StartsWith(prefix)
                            && !EF.Functions.Like(f.FilePath, prefix + "%/%"))
                .Select(f => new { f.FilePath, f.Chapter.Volume.SeriesId })
                .ToListAsync(ct);

            var seriesIds = filesInFolder
                .Where(f => f.FilePath.FolderOf() == folder.Key)
                .Select(f => f.SeriesId)
                .Distinct()
                .ToList();
            if (seriesIds.Count != 1) continue;

            foreach (var row in folder)
            {
                row.SeriesId = seriesIds[0];
            }
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
