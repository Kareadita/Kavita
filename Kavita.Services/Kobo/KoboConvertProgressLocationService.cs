using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.Models.Entities;
using Kavita.Models.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Kobo;

/// <summary>
/// Convert-chapter progress-win Location upsert/clear (PagesRead ↔ factual KEPUB Location).
/// </summary>
public interface IKoboConvertProgressLocationService
{
    bool IsConvertChapter(Chapter chapter);

    /// <summary>
    /// Cached KEPUB path when present and spine length matches <see cref="Chapter.Pages"/>; otherwise null.
    /// </summary>
    string? TryResolveTrustedKepubPath(Chapter chapter);

    Task UpsertFromPagesReadAsync(int userId, Chapter chapter, int pagesRead, bool readyToRead,
        CancellationToken ct = default);

    Task ClearLocationAsync(int userId, int chapterId, CancellationToken ct = default);

    Task UpsertLocationAsync(int userId, int chapterId, string? value, string? type, string? source,
        CancellationToken ct = default);
}

public class KoboConvertProgressLocationService(
    ILogger<KoboConvertProgressLocationService> logger,
    IUnitOfWork unitOfWork,
    IKoboConversionService koboConversionService)
    : IKoboConvertProgressLocationService
{
    public bool IsConvertChapter(Chapter chapter)
    {
        ArgumentNullException.ThrowIfNull(chapter);
        return KoboService.PreferNativeEpub(chapter.Files) == null
               && KoboService.PreferConvertibleArchive(chapter.Files) != null;
    }

    public string? TryResolveTrustedKepubPath(Chapter chapter)
    {
        ArgumentNullException.ThrowIfNull(chapter);
        var archive = KoboService.PreferConvertibleArchive(chapter.Files);
        if (archive == null) return null;

        var path = koboConversionService.TryGetCachedKepubPath(chapter.Id, archive);
        if (path == null || !File.Exists(path)) return null;

        if (chapter.Pages <= 0) return null;
        var spine = KoboConvertEpubInspector.TryCountSpinePages(path);
        if (spine != chapter.Pages)
        {
            logger.LogDebug(
                "Convert KEPUB page-count untrusted for chapter {ChapterId}: spine={Spine}, Pages={Pages}",
                chapter.Id, spine, chapter.Pages);
            return null;
        }

        return path;
    }

    public async Task UpsertFromPagesReadAsync(int userId, Chapter chapter, int pagesRead, bool readyToRead,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(chapter);

        var kepub = TryResolveTrustedKepubPath(chapter);
        if (kepub == null)
        {
            await ClearLocationAsync(userId, chapter.Id, ct);
            return;
        }

        var mapped = KoboConvertLocationCodec.TryEncode(pagesRead, chapter.Pages, readyToRead);
        if (mapped == null)
        {
            await ClearLocationAsync(userId, chapter.Id, ct);
            return;
        }

        await UpsertLocationAsync(userId, chapter.Id, mapped.Value, mapped.Type, mapped.Source, ct);
    }

    public async Task ClearLocationAsync(int userId, int chapterId, CancellationToken ct = default)
    {
        var locationRow = await unitOfWork.DataContext.AppUserKoboReadingLocation
            .FirstOrDefaultAsync(l => l.AppUserId == userId && l.ChapterId == chapterId, ct);
        if (locationRow == null) return;

        locationRow.LocationValue = null;
        locationRow.LocationType = null;
        locationRow.LocationSource = null;
    }

    public async Task UpsertLocationAsync(int userId, int chapterId, string? value, string? type, string? source,
        CancellationToken ct = default)
    {
        var locationRow = await unitOfWork.DataContext.AppUserKoboReadingLocation
            .FirstOrDefaultAsync(l => l.AppUserId == userId && l.ChapterId == chapterId, ct);
        if (locationRow == null)
        {
            if (string.IsNullOrEmpty(value)) return;
            unitOfWork.DataContext.AppUserKoboReadingLocation.Add(new AppUserKoboReadingLocation
            {
                AppUserId = userId,
                ChapterId = chapterId,
                LocationValue = value,
                LocationType = type,
                LocationSource = source,
            });
            return;
        }

        locationRow.LocationValue = value;
        locationRow.LocationType = type;
        locationRow.LocationSource = source;
    }
}
