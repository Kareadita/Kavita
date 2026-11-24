using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.Kobo;
using Kavita.API.Services.Reading;
using Kavita.Common;
using Kavita.Models.DTOs;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Images;
using Kavita.Models.Kobo;
using Kavita.Services.Images.ImageSteps;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Kobo;

/// <summary>
/// Handles all things Kobo Optimization (Archive -> Epub Optimization)
/// </summary>
public class KoboOptimizationService(ICacheService cacheService,
    ILocalizationService localizationService,
    IDirectoryService directoryService,
    IKepubifyConverterService kepubifyConverterService,
    IFileSystem fileSystem,
    IEntityNamingService namingService,
    IUnitOfWork unitOfWork,
    IEpubBuilderService epubBuilderService,
    IReadingProfileService readingProfileService,
    ILogger<KoboOptimizationService> logger) : IKoboOptimizationService
{
    // Device Profile, Direction, Steps, Kobo Cover and Cached kepub file all belong here
    // EpubBuilder responsible for turning into an Epub (agnostic to Kobo)


    /// <summary>
    /// Generates a Kobo-compatible file for downloading
    /// </summary>
    public async Task<string> GetKoboDownloadPathAsync(int userId, int chapterId, bool convert, string? koboDeviceId, CancellationToken ct = default)
    {
        var chapter = await unitOfWork.ChapterRepository.GetChapterDtoAsync(chapterId, userId, ct);
        if (chapter == null || chapter.Files == null || chapter.Files.Count == 0)
        {
            throw new KavitaException("Chapter or files don't exist");
        }

        return chapter.Files.First().Format switch
        {
            MangaFormat.Epub => await GetEpubDownloadPath(chapter, convert, ct),
            MangaFormat.Archive or MangaFormat.Image or MangaFormat.Pdf => await GetImageDownloadPath(userId, chapter, convert, koboDeviceId, ct),
            _ => throw new NotSupportedException()
        };
    }

    private async Task<string> GetEpubDownloadPath(ChapterDto chapter, bool convert, CancellationToken ct)
    {
        if (chapter.Files.Count > 1) throw new NotSupportedException("Kavita doesn't support multi-part epub files");

        var epubPath = chapter.Files.First().FilePath;
        if (!convert) return epubPath;

        // KoboSyncService reads annotations and progress from this kepub, so it must stay on BuildKepubifyPath
        return await ConvertToKepub(epubPath, kepubifyConverterService.BuildKepubifyPath(chapter.Id), ct);
    }

    private async Task<string> GetImageDownloadPath(int userId, ChapterDto chapter, bool convert, string? koboDeviceId,
        CancellationToken ct)
    {
        // TODO: make this less painful
        //var (volumeId, seriesId, libraryId) = await unitOfWork.ChapterRepository.GetParentIdsForChapter(chapter.Id, ct);
        var libraryType = await unitOfWork.ChapterRepository.GetLibraryTypeAsync(chapter.Id, ct);
        var volumeDto = await unitOfWork.VolumeRepository.GetVolumeDtoAsync(chapter.VolumeId, userId, ct);
        var seriesDto = await unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(volumeDto!.SeriesId, userId, ct: ct);
        var readingProfile = await readingProfileService.GetReadingProfileDtoForSeries(userId, seriesDto!.LibraryId, volumeDto.SeriesId, null, ct: ct); // active device id lookup

        var namingCtx = await LocalizedNamingContext.CreateAsync(namingService, localizationService, userId, libraryType!.Value);
        var title = namingCtx.BuildChapterTitle(volumeDto, chapter);

        var renderSettings = await GetRenderer(userId, koboDeviceId);
        var cacheKey = BuildCacheKey(chapter, title, readingProfile.ReadingDirection, namingCtx, renderSettings);
        var basePath = Path.Join(directoryService.TempDirectory, $"kavita_kobo_c{chapter.Id}_{cacheKey}");
        var epubPath = $"{basePath}.epub";
        var kepubPath = $"{basePath}.kepub.epub";

        if (convert && fileSystem.File.Exists(kepubPath)) return kepubPath;

        if (!fileSystem.File.Exists(epubPath))
        {
            if (await cacheService.Ensure(chapter.Id, true, ct) is null)
            {
                throw new KavitaException(await localizationService.TranslateAsync("cache-file-find"));
            }

            // Built under a temp name and moved, so a failed build never leaves a half-written epub that the Exists check would serve
            var buildPath = $"{basePath}.{Guid.NewGuid():N}.tmp.epub";
            var options = new BuildEpubOptions(title, cacheService.GetCachedPages(chapter.Id).ToList(), renderSettings,
                readingProfile.ReadingDirection, chapter, buildPath);

            try
            {
                await epubBuilderService.BuildEpubAsync(options, userId, namingCtx, ct);
                fileSystem.File.Move(buildPath, epubPath, true);
            }
            catch
            {
                directoryService.DeleteFileQuietly(buildPath);
                throw;
            }
        }

        return convert ? await ConvertToKepub(epubPath, kepubPath, ct) : epubPath;
    }

    private Task<ImageRender> GetRenderer(int userId, string? koboDeviceId)
    {
        // TODO: Given the koboDeviceId, get the Kobo Capabilities
        var deviceProfile = new KoboDeviceProfile
        {
            Name = "Libra 2",
            Width = 1264,
            Height = 1680,
            IsGreyscale = true
        };

        List<IImageStep> steps = [new FitToStep(deviceProfile.Width, deviceProfile.Height)];
        if (deviceProfile.IsGreyscale)
        {
            steps.Add(new GreyscaleStep());
        }

        return Task.FromResult(new ImageRender(EncodeFormat.JPEG, steps, 90));
    }

    /// <summary>
    /// Builds a cache key based on everything that may change the underlying compiled file.
    /// </summary>
    private string BuildCacheKey(ChapterDto chapter, string title, ReadingDirection readingDirection,
        LocalizedNamingContext naming, ImageRender render)
    {
        IEnumerable<string> parts =
        [
            $"c{chapter.Id}",
            chapter.LastModifiedUtc.ToString("O"),
            title,
            readingDirection.ToString(),
            naming.VolumeLabel,
            naming.ChapterLabel,
            naming.IssueLabel,
            naming.BookLabel,
            render.Format.ToString(),
            render.Quality?.ToString() ?? "default",
            ..render.Steps.Select(s => s.ToString() ?? string.Empty)
        ];
        var key = string.Join('|', parts);

        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16];
        logger.LogDebug("Kobo file {Hash} for chapter {ChapterId} is built from {CacheKey}", hash, chapter.Id, key);

        return hash;
    }

    private async Task<string> ConvertToKepub(string epubPath, string kepubPath, CancellationToken ct)
    {
        var converted = await kepubifyConverterService.ConvertEpubToKepub(epubPath, kepubPath, ct);

        return converted ? kepubPath : epubPath;
    }
}
