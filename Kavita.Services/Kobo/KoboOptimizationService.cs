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

        var format = chapter.Files.First().Format;

        var outputPath = format switch
        {
            MangaFormat.Epub => CollectEpubFiles(chapter),
            MangaFormat.Archive or MangaFormat.Image or MangaFormat.Pdf => await CollectImageFiles(userId, chapter, koboDeviceId, ct),
            _ => throw new NotSupportedException()
        };

        // Call kepubify and return its path
        if (convert)
        {
            return await ConvertToKepub(chapterId, outputPath, ct);
        }

        return outputPath;
    }

    private async Task<string> CollectImageFiles(int userId, ChapterDto chapter, string? koboDeviceId,
        CancellationToken ct = default)
    {
        var cachedChapter = await cacheService.Ensure(chapter.Id, true, ct);
        if (cachedChapter is null)
        {
            throw new KavitaException(await localizationService.TranslateAsync("cache-file-find"));
        }

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
        var outputPath = Path.Join(directoryService.TempDirectory, $"kavita_kobo_c{chapter.Id}_{cacheKey}.epub");

        var options = new BuildEpubOptions(title, cacheService.GetCachedPages(chapter.Id).ToList(), renderSettings,
            readingProfile.ReadingDirection, chapter, outputPath);

        return await epubBuilderService.BuildEpubAsync(options, userId, namingCtx, ct);
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

    private static string CollectEpubFiles(ChapterDto chapter)
    {
        if (chapter.Files.Count > 1) throw new NotSupportedException("Kavita doesn't support multi-part epub files");

        return chapter.Files.First().FilePath;
    }

    private async Task<string> ConvertToKepub(int chapterId, string pathToEpub, CancellationToken ct)
    {
        var outputPath = kepubifyConverterService.BuildKepubifyPath(chapterId);

        var converted = await kepubifyConverterService.ConvertEpubToKepub(pathToEpub, outputPath, ct);

        return converted ? outputPath : pathToEpub;
    }
}
