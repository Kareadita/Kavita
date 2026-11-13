using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyCaching.Core;
using Flurl;
using Flurl.Http;
using HtmlAgilityPack;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.Metadata;
using Kavita.API.Services.SignalR;
using Kavita.Common;
using Kavita.Common.Extensions;
using Kavita.Common.Helpers;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Person;
using Kavita.Models.Entities.User;
using Kavita.Models.Images;
using Kavita.Services.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Metadata;

public class CoverDbService : ICoverDbService
{
    private readonly ILogger<CoverDbService> _logger;
    private readonly IDirectoryService _directoryService;
    private readonly IEasyCachingProviderFactory _cacheFactory;
    private readonly IHostEnvironment _env;
    private readonly IImageService _imageService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventHub _eventHub;
    private readonly IImagePipeline _imagePipeline;
    private readonly TimeSpan _cacheTime = TimeSpan.FromDays(10);

    private const string NewHost = "https://www.kavitareader.com/CoversDB/";

    private static readonly string[] ValidIconRelations = {
        "icon",
        "apple-touch-icon",
        "apple-touch-icon-precomposed",
        "apple-touch-icon icon-precomposed" // ComicVine has it combined
    };

    /// <summary>
    /// A mapping of urls that need to get the icon from another url, due to strangeness (like app.plex.tv loading a black icon)
    /// </summary>
    private static readonly Dictionary<string, string> FaviconUrlMapper = new()
    {
        ["https://app.plex.tv"] = "https://plex.tv"
    };
    /// <summary>
    /// Cache of the publisher/favicon list
    /// </summary>
    private static readonly TimeSpan CacheDuration = TimeSpan.FromDays(1);

    private readonly IUrlValidationService _urlValidationService;

    public CoverDbService(ILogger<CoverDbService> logger, IDirectoryService directoryService,
        IEasyCachingProviderFactory cacheFactory, IHostEnvironment env, IImageService imageService,
        IUnitOfWork unitOfWork, IEventHub eventHub, IUrlValidationService urlValidationService, IImagePipeline imagePipeline)
    {
        _imagePipeline = imagePipeline;
        _logger = logger;
        _directoryService = directoryService;
        _cacheFactory = cacheFactory;
        _env = env;
        _imageService = imageService;
        _unitOfWork = unitOfWork;
        _eventHub = eventHub;
        _urlValidationService = urlValidationService;
    }

    /// <summary>
    /// Downloads the favicon image from a given website URL, optionally falling back to a custom method if standard methods fail.
    /// </summary>
    /// <param name="url">The full URL of the website to extract the favicon from.</param>
    /// <param name="encodeFormat">The desired image encoding format for saving the favicon (e.g., WebP, PNG).</param>
    /// <param name="ct"></param>
    /// <returns>
    /// A string representing the filename of the downloaded favicon image, saved to the configured favicon directory.
    /// </returns>
    /// <exception cref="KavitaException">
    /// Thrown when favicon retrieval fails or if a previously failed domain is detected in cache.
    /// </exception>
    /// <remarks>
    /// This method first checks for a cached failure to avoid re-requesting bad links.
    /// It then attempts to parse HTML for `link` tags pointing to `.png` favicons and
    /// falls back to an internal fallback method if needed. Valid results are saved to disk.
    /// </remarks>
    public async Task<string> DownloadFaviconAsync(string url, EncodeFormat encodeFormat, CancellationToken ct = default)
    {
        await _urlValidationService.ValidateUrlAsync(url);

        // Parse the URL to get the domain (including subdomain)
        var uri = new Uri(url);
        var domain = uri.Host.Replace(Environment.NewLine, string.Empty);
        var baseUrl = uri.Scheme + "://" + uri.Host;


        var provider = _cacheFactory.GetCachingProvider(EasyCacheProfiles.Favicon);
        var res = await provider.GetAsync<string>(baseUrl, ct);
        if (res.HasValue)
        {
            var sanitizedBaseUrl = baseUrl.Sanitize();
            _logger.LogTrace("Kavita has already tried to fetch from {BaseUrl} and failed. Skipping duplicate check", sanitizedBaseUrl);
            throw new KavitaException($"Kavita has already tried to fetch from {sanitizedBaseUrl} and failed. Skipping duplicate check");
        }

        await provider.SetAsync(baseUrl, string.Empty, _cacheTime, ct);
        if (FaviconUrlMapper.TryGetValue(baseUrl, out var value))
        {
            url = value;
        }

        var correctSizeLink = string.Empty;

        try
        {
            var htmlContent = await FlurlConfiguration.CreateSafeRequest(url)
                .GetStringAsync(cancellationToken: ct);
            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(htmlContent);

            var pngLinks = htmlDocument.DocumentNode.Descendants("link")
                .Where(link => ValidIconRelations.Contains(link.GetAttributeValue("rel", string.Empty)))
                .Select(link => link.GetAttributeValue("href", string.Empty))
                .Where(href => href.Split("?")[0].EndsWith(".png", StringComparison.InvariantCultureIgnoreCase))
                .ToList();

            correctSizeLink = (pngLinks?.Find(pngLink => pngLink.Contains("32")) ?? pngLinks?.FirstOrDefault());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading favicon.png for {Domain}, will try fallback methods", domain);
        }

        try
        {
            if (string.IsNullOrEmpty(correctSizeLink))
            {
                correctSizeLink = await FallbackToKavitaReaderFavicon(baseUrl);
            }
            if (string.IsNullOrEmpty(correctSizeLink))
            {
                throw new KavitaException($"Could not grab favicon from {baseUrl}");
            }

            var finalUrl = correctSizeLink;

            // If starts with //, it's coming usually from an offsite cdn
            if (correctSizeLink.StartsWith("//"))
            {
                finalUrl = "https:" + correctSizeLink;
            }
            else if (!correctSizeLink.StartsWith(uri.Scheme))
            {
                finalUrl = Url.Combine(baseUrl, correctSizeLink);
            }

            _logger.LogTrace("Fetching favicon from {Url}", finalUrl);
            var filename = await DownloadAndWriteAsync(finalUrl, ImageKind.Favicon, new ImagePolicy(encodeFormat, CoverImageSize.Default),
                _directoryService.FaviconDirectory, ImageService.GetWebLinkFormat(baseUrl, encodeFormat), ct);
            _logger.LogDebug("Favicon for {Domain} downloaded and saved successfully", domain);

            return filename;
        } catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading favicon for {Domain}", domain);
            throw;
        }
    }

    public async Task<string> DownloadPublisherImageAsync(string publisherName, EncodeFormat encodeFormat,
        CancellationToken ct = default)
    {
        try
        {
            // Sanitize user input
            publisherName = publisherName.Replace(Environment.NewLine, string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);
            var provider = _cacheFactory.GetCachingProvider(EasyCacheProfiles.Publisher);
            var res = await provider.GetAsync<string>(publisherName, ct);
            if (res.HasValue)
            {
                _logger.LogDebug("Kavita has already tried to fetch Publisher: {PublisherName} and failed. Skipping duplicate check", publisherName);
                // Do not throw to prevent duplicate log spam when visiting series
                return string.Empty;
            }

            await provider.SetAsync(publisherName, string.Empty, _cacheTime, ct);
            var publisherLink = await FallbackToKavitaReaderPublisher(publisherName);
            if (string.IsNullOrEmpty(publisherLink))
            {
                throw new KavitaException($"Could not grab publisher image for {publisherName}");
            }

            _logger.LogTrace("Fetching publisher image from {Url}", publisherLink.Sanitize());
            var filename = await DownloadAndWriteAsync(publisherLink, ImageKind.Publisher, new ImagePolicy(encodeFormat, CoverImageSize.Default),
                _directoryService.PublisherDirectory, ImageService.GetPublisherFormat(publisherName, encodeFormat), ct);

            _logger.LogDebug("Publisher image for {PublisherName} downloaded and saved successfully", publisherName.Sanitize());

            return filename;
        } catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading image for {PublisherName}", publisherName.Sanitize());
            throw;
        }
    }

    /// <summary>
    /// Downloads <paramref name="url"/> as is into the temp directory
    /// </summary>
    /// <returns>Full path of the downloaded file. The caller deletes it</returns>
    private async Task<string> DownloadImageFromUrl(string url, CancellationToken ct)
    {
        await _urlValidationService.ValidateUrlAsync(url);

        var path = NewTempPath();
        _logger.LogTrace("Fetching image from {Url}", url.Sanitize());
        await FlurlConfiguration.DownloadToFileAsync(url, path, ControllerConstants.MaxUploadSizeBytes, ct);

        return path;
    }

    private async Task<string> DownloadAndWriteAsync(string url, ImageKind kind, ImagePolicy policy, string directory,
        string fileName, CancellationToken ct)
    {
        var sourcePath = await DownloadImageFromUrl(url, ct);
        try
        {
            return _imagePipeline.Write(new ImageSource.FromFile(sourcePath), kind, policy, directory, fileName).FileName;
        }
        finally
        {
            _directoryService.DeleteFiles([sourcePath]);
        }
    }

    /// <summary>
    /// Puts the full-size source for a cover into the temp directory, so it can be compared before it is thumbnailed
    /// </summary>
    /// <returns>Full path of the source file. The caller deletes it</returns>
    private async Task<string> StageCoverSource(string url, bool fromBase64, CancellationToken ct)
    {
        if (!fromBase64) return await DownloadImageFromUrl(url, ct);

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(url);
        }
        catch (FormatException e)
        {
            throw new KavitaException("Invalid Base64 string", e);
        }

        var path = NewTempPath();
        await File.WriteAllBytesAsync(path, bytes, ct);

        return path;
    }

    private async Task<string> WriteCover(string sourcePath, ImageKind kind, string fileName, CancellationToken ct)
    {
        var settings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        var policy = new ImagePolicy(settings.EncodeMediaAs, settings.CoverImageSize);

        return _imagePipeline.Write(new ImageSource.FromFile(sourcePath), kind, policy,
            _directoryService.CoverImageDirectory, fileName).FileName;
    }

    private string NewTempPath()
    {
        return Path.Join(_directoryService.TempDirectory, $"download_{Guid.NewGuid():N}.tmp");
    }

    public async Task<string?> GetPersonImageUrlAsync(Person person, CancellationToken ct = default)
    {
        try
        {
            return await GetCoverPersonImagePath(person);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error looking up CoversDB image for {PersonName}", person.Name);
        }

        return null;
    }

    private async Task<string?> GetCoverPersonImagePath(Person person, CancellationToken ct = default)
    {
        var tempFile = Path.Join(_directoryService.LongTermCacheDirectory, "people.yml");

        // Check if the file already exists and skip download in Development environment
        if (File.Exists(tempFile))
        {
            if (_env.IsDevelopment())
            {
                _logger.LogInformation("Using existing people.yml file in Development environment");
            }
            else
            {
                // Remove file if not in Development and file is older than 7 days
                if (File.GetLastWriteTime(tempFile) < DateTime.Now.AddDays(-7))
                {
                    File.Delete(tempFile);
                }
            }
        }

        // Download the file if it doesn't exist or was deleted due to age
        if (!File.Exists(tempFile))
        {
            var masterPeopleFile = await $"{NewHost}people/people.yml"
                .DownloadFileAsync(_directoryService.LongTermCacheDirectory, cancellationToken: ct);

            if (!File.Exists(tempFile) || string.IsNullOrEmpty(masterPeopleFile))
            {
                _logger.LogError("Could not download people.yml from Github");
                return null;
            }
        }


        var coverDbRepository = new CoverDbRepository(tempFile);

        var coverAuthor = coverDbRepository.FindBestAuthorMatch(person);
        if (coverAuthor == null || string.IsNullOrEmpty(coverAuthor.ImagePath))
        {
            throw new KavitaException($"Could not grab person image for {person.Name}");
        }

        return $"{NewHost}{coverAuthor.ImagePath}";
    }

    private async Task<string> FallbackToKavitaReaderFavicon(string baseUrl)
    {
        const string urlsFileName = "urls.txt";
        var correctSizeLink = string.Empty;
        var allOverrides = await GetCachedData(urlsFileName) ??
                           await $"{NewHost}favicons/{urlsFileName}".GetStringAsync();

        // Cache immediately
        await CacheDataAsync(urlsFileName, allOverrides);


        if (string.IsNullOrEmpty(allOverrides)) return correctSizeLink;

        var cleanedBaseUrl = baseUrl.Replace("https://", string.Empty);
        var externalFile = allOverrides
            .Split("\n")
            .Select(url => url.Trim('\n', '\r')) // Ensure windows line terminators don't mess anything up
            .FirstOrDefault(url =>
                cleanedBaseUrl.Equals(url.Replace(".png", string.Empty)) ||
                cleanedBaseUrl.Replace("www.", string.Empty).Equals(url.Replace(".png", string.Empty)
                ));

        if (string.IsNullOrEmpty(externalFile))
        {
            throw new KavitaException($"Could not grab favicon from {baseUrl.Sanitize()}");
        }

        return $"{NewHost}favicons/{externalFile}";
    }

    private async Task<string> FallbackToKavitaReaderPublisher(string publisherName)
    {
        const string publisherFileName = "publishers.txt";
        var allOverrides = await GetCachedData(publisherFileName) ??
                           await $"{NewHost}publishers/{publisherFileName}".GetStringAsync();

        // Cache immediately
        await CacheDataAsync(publisherFileName, allOverrides);

        if (string.IsNullOrEmpty(allOverrides)) return string.Empty;

        var externalFile = allOverrides
            .Split("\n")
            .Select(url => url.Trim('\n', '\r')) // Ensure windows line terminators don't mess anything up
            .Select(publisherLine =>
            {
                var tokens = publisherLine.Split("|");
                if (tokens.Length != 2) return null;
                var aliases = tokens[0];
                // Multiple publisher aliases are separated by #
                if (aliases.Split("#").Any(name => name.ToLowerInvariant().Trim().Equals(publisherName.ToLowerInvariant().Trim())))
                {
                    return tokens[1];
                }
                return null;
            })
            .FirstOrDefault(url => !string.IsNullOrEmpty(url));

        if (string.IsNullOrEmpty(externalFile))
        {
            throw new KavitaException($"Could not grab publisher image for {publisherName}");
        }

        return $"{NewHost}publishers/{externalFile}";
    }

    private async Task CacheDataAsync(string fileName, string? content)
    {
        if (content == null) return;

        try
        {
            var filePath = _directoryService.FileSystem.Path.Join(_directoryService.LongTermCacheDirectory, fileName);
            await File.WriteAllTextAsync(filePath, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache {FileName}", fileName);
        }
    }


    private async Task<string?> GetCachedData(string cacheFile)
    {
        // Form the full file path:
        var filePath = _directoryService.FileSystem.Path.Join(_directoryService.LongTermCacheDirectory, cacheFile);
        if (!File.Exists(filePath)) return null;

        var fileInfo = new FileInfo(filePath);
        if (DateTime.UtcNow - fileInfo.LastWriteTimeUtc <= CacheDuration)
        {
            return await File.ReadAllTextAsync(filePath);
        }

        return null;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="person"></param>
    /// <param name="url"></param>
    /// <param name="fromBase64"></param>
    /// <param name="checkNoImagePlaceholder">Will check against all known null image placeholders to avoid writing it</param>
    /// <param name="chooseBetterImage">If we check cross-reference the current cover for the better option</param>
    /// <param name="ct"></param>
    public async Task SetPersonCoverByUrl(Person person, string url, bool fromBase64 = true,
        bool checkNoImagePlaceholder = false, bool chooseBetterImage = true, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(url))
        {
            var finalFileName = ImageService.GetPersonFormat(person.Id) + ".webp";
            var finalFullPath = Path.Combine(_directoryService.CoverImageDirectory, finalFileName);
            var sourcePath = await StageCoverSource(url, fromBase64, ct);
            string? staleCover = null;

            try
            {
                // Skip setting image if it's similar to a known placeholder
                if (checkNoImagePlaceholder)
                {
                    var placeholderPath = Path.Combine(_directoryService.AssetsDirectory, "anilist-no-image-placeholder.jpg");
                    var similarity = placeholderPath.CalculateSimilarity(sourcePath);
                    if (similarity >= 0.9f)
                    {
                        _logger.LogInformation("Skipped setting placeholder image for person {PersonId} due to high similarity ({Similarity})", person.Id, similarity);
                        return;
                    }
                }

                try
                {
                    if (!string.IsNullOrEmpty(person.CoverImage) && chooseBetterImage)
                    {
                        var existingPath = Path.Combine(_directoryService.CoverImageDirectory, person.CoverImage);

                        if (!File.Exists(existingPath))
                        {
                            _logger.LogWarning("Existing cover for Person {PersonId} is missing on disk ({Path}); accepting new cover from source", person.Id, existingPath);
                        }
                        else
                        {
                            var betterImage = existingPath.GetBetterImage(sourcePath)!;
                            if (!string.Equals(betterImage, sourcePath, StringComparison.OrdinalIgnoreCase)) return;

                            staleCover = existingPath;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error choosing better image for Person: {PersonId}; accepting new cover from source", person.Id);
                }

                person.CoverImage = await WriteCover(sourcePath, ImageKind.PersonCover, finalFileName, ct);
                DeleteReplacedCover(staleCover, finalFullPath);
            }
            finally
            {
                _directoryService.DeleteFiles([sourcePath]);
            }

            person.CoverImageLocked = true;
            _imageService.UpdateColorScape(person);
            _unitOfWork.PersonRepository.Update(person);
        }
        else
        {
            person.CoverImage = string.Empty;
            person.CoverImageLocked = false;
            _imageService.UpdateColorScape(person);
            _unitOfWork.PersonRepository.Update(person);
        }

        if (_unitOfWork.HasChanges())
        {
            await _unitOfWork.CommitAsync(ct);
            await _eventHub.SendMessageAsync(MessageFactory.CoverUpdate,
                MessageFactory.CoverUpdateEvent(person.Id, MessageFactoryEntityTypes.Person), false, ct);
        }
    }

    /// <summary>
    /// Sets the series cover by url
    /// </summary>
    /// <param name="series"></param>
    /// <param name="url"></param>
    /// <param name="fromBase64"></param>
    /// <param name="chooseBetterImage">If images are similar, will choose the higher quality image</param>
    /// <param name="ct"></param>
    public async Task SetSeriesCoverByUrl(Series series, string url, bool fromBase64 = true,
        bool chooseBetterImage = false, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(url))
        {
            var finalFileName = ImageService.GetSeriesFormat(series.Id) + ".webp";
            var finalFullPath = Path.Combine(_directoryService.CoverImageDirectory, finalFileName);
            var sourcePath = await StageCoverSource(url, fromBase64, ct);
            string? staleCover = null;

            try
            {
                if (chooseBetterImage && !string.IsNullOrEmpty(series.CoverImage))
                {
                    var existingPath = Path.Combine(_directoryService.CoverImageDirectory, series.CoverImage);

                    if (!File.Exists(existingPath))
                    {
                        // Existing cover reference is stale/missing on disk; accept the new image as it's better than nothing
                        _logger.LogWarning("Existing cover for Series {SeriesId} is missing on disk ({Path}); accepting new cover from source", series.Id, existingPath);
                    }
                    else
                    {
                        try
                        {
                            var betterImage = existingPath.GetBetterImage(sourcePath)!;
                            if (!string.Equals(betterImage, sourcePath, StringComparison.OrdinalIgnoreCase)) return;

                            // Don't delete the Series cover unless it is an override, otherwise the first chapter will be null
                            if (existingPath.Contains(ImageService.GetSeriesFormat(series.Id)))
                            {
                                staleCover = existingPath;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error choosing better image for Series: {SeriesId}; accepting new cover from source", series.Id);
                        }
                    }
                }

                series.CoverImage = await WriteCover(sourcePath, ImageKind.SeriesCover, finalFileName, ct);
                DeleteReplacedCover(staleCover, finalFullPath);
            }
            finally
            {
                _directoryService.DeleteFiles([sourcePath]);
            }

            series.CoverImageLocked = true;
            _imageService.UpdateColorScape(series);
            _unitOfWork.SeriesRepository.Update(series);
        }
        else
        {
            series.CoverImage = null;
            series.CoverImageLocked = false;
            _logger.LogDebug("[SeriesCoverImageBug] Setting Series Cover Image to null");
            _imageService.UpdateColorScape(series);
            _unitOfWork.SeriesRepository.Update(series);
        }

        if (_unitOfWork.HasChanges())
        {
            await _unitOfWork.CommitAsync(ct);
            await _eventHub.SendMessageAsync(MessageFactory.CoverUpdate,
                MessageFactory.CoverUpdateEvent(series.Id, MessageFactoryEntityTypes.Series), false, ct);
        }
    }

    // TODO: Refactor this to IHasCoverImage instead of a hard entity type
    public async Task SetChapterCoverByUrl(Chapter chapter, string url, bool fromBase64 = true,
        bool chooseBetterImage = false, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(url))
        {
            var finalFileName = ImageService.GetChapterFormat(chapter.Id, chapter.VolumeId) + ".webp";
            var finalFullPath = Path.Combine(_directoryService.CoverImageDirectory, finalFileName);
            var sourcePath = await StageCoverSource(url, fromBase64, ct);
            string? staleCover = null;

            try
            {
                if (chooseBetterImage && !string.IsNullOrEmpty(chapter.CoverImage))
                {
                    var existingPath = Path.Combine(_directoryService.CoverImageDirectory, chapter.CoverImage);

                    if (!File.Exists(existingPath))
                    {
                        // Existing cover reference is stale/missing on disk; accept the new image as it's better than nothing
                        _logger.LogWarning("Existing cover for Chapter {FileName} ({ChapterId}) is missing on disk ({Path}); accepting new cover from source", chapter.Range, chapter.Id, new FileInfo(existingPath).Name);
                    }
                    else
                    {
                        try
                        {
                            var betterImage = existingPath.GetBetterImage(sourcePath)!;
                            if (!string.Equals(betterImage, sourcePath, StringComparison.OrdinalIgnoreCase)) return;

                            staleCover = existingPath;
                        }
                        catch (Exception ex)
                        {
                            // Accept the new image rather than keeping no/old cover, since a source is better than nothing
                            _logger.LogWarning(ex, "There was an issue trying to choose a better cover image for Chapter: {FileName} ({ChapterId}); accepting new cover from source", chapter.Range, chapter.Id);
                        }
                    }
                }

                chapter.CoverImage = await WriteCover(sourcePath, ImageKind.ChapterCover, finalFileName, ct);
                DeleteReplacedCover(staleCover, finalFullPath);
            }
            finally
            {
                _directoryService.DeleteFiles([sourcePath]);
            }

            chapter.CoverImageLocked = true;
            _imageService.UpdateColorScape(chapter);
            _unitOfWork.ChapterRepository.Update(chapter);
        }
        else
        {
            chapter.CoverImage = null;
            chapter.CoverImageLocked = false;
            _imageService.UpdateColorScape(chapter);
            _unitOfWork.ChapterRepository.Update(chapter);
        }

        if (_unitOfWork.HasChanges())
        {
            await _unitOfWork.CommitAsync(ct);
            await _eventHub.SendMessageAsync(
                MessageFactory.CoverUpdate,
                MessageFactory.CoverUpdateEvent(chapter.Id, MessageFactoryEntityTypes.Chapter),
                false, ct);
        }
    }

    public async Task SetVolumeCoverByUrl(Volume volume, string url, bool fromBase64 = true,
        bool chooseBetterImage = false, CancellationToken ct = default)
    {
        // TODO: See what we can refactor with SetChapterCoverByUrl
        if (!string.IsNullOrEmpty(url))
        {
            var finalFileName = ImageService.GetVolumeFormat(volume.Id) + ".webp";
            var finalFullPath = Path.Combine(_directoryService.CoverImageDirectory, finalFileName);
            var sourcePath = await StageCoverSource(url, fromBase64, ct);
            string? staleCover = null;

            try
            {
                if (chooseBetterImage && !string.IsNullOrEmpty(volume.CoverImage))
                {
                    var existingPath = Path.Combine(_directoryService.CoverImageDirectory, volume.CoverImage);

                    if (!File.Exists(existingPath))
                    {
                        // Existing cover reference is stale/missing on disk; accept the new image as it's better than nothing
                        _logger.LogWarning("Existing cover for Volume {VolumeName} ({VolumeId}) is missing on disk ({Path}); accepting new cover from source", volume.Name, volume.Id, new FileInfo(existingPath).Name);
                    }
                    else
                    {
                        try
                        {
                            var betterImage = existingPath.GetBetterImage(sourcePath)!;
                            if (!string.Equals(betterImage, sourcePath, StringComparison.OrdinalIgnoreCase)) return;

                            // Only delete existing when it is the volume's own file; a volume can show its chapter's file
                            if (!ImageService.IsChapterFormatForVolume(existingPath, volume.Id))
                            {
                                staleCover = existingPath;
                            }
                        }
                        catch (Exception ex)
                        {
                            // Accept the new image rather than keeping no/old cover, since a source is better than nothing
                            _logger.LogWarning(ex, "There was an issue trying to choose a better cover image for Volume: {VolumeName} ({VolumeId}); accepting new cover from source", volume.Name, volume.Id);
                        }
                    }
                }

                volume.CoverImage = await WriteCover(sourcePath, ImageKind.VolumeCover, finalFileName, ct);
                DeleteReplacedCover(staleCover, finalFullPath);
            }
            finally
            {
                _directoryService.DeleteFiles([sourcePath]);
            }

            volume.CoverImageLocked = true;
            _imageService.UpdateColorScape(volume);
            _unitOfWork.VolumeRepository.Update(volume);
        }
        else
        {
            volume.CoverImage = null;
            volume.CoverImageLocked = false;
            _imageService.UpdateColorScape(volume);
            _unitOfWork.VolumeRepository.Update(volume);
        }

        if (_unitOfWork.HasChanges())
        {
            await _unitOfWork.CommitAsync(ct);
            await _eventHub.SendMessageAsync(
                MessageFactory.CoverUpdate,
                MessageFactory.CoverUpdateEvent(volume.Id, MessageFactoryEntityTypes.Volume),
                false, ct);
        }
    }

    public async Task SetVolumeCoverFromChapter(Volume volume, Chapter chapter, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(chapter.CoverImage)) return;

        if (!string.Equals(volume.CoverImage, chapter.CoverImage, StringComparison.Ordinal))
        {
            volume.CoverImage = chapter.CoverImage;
            volume.CoverImageLocked = true;
            _imageService.UpdateColorScape(volume);
            _unitOfWork.VolumeRepository.Update(volume);

            if (_unitOfWork.HasChanges())
            {
                await _unitOfWork.CommitAsync(ct);
                await _eventHub.SendMessageAsync(
                    MessageFactory.CoverUpdate,
                    MessageFactory.CoverUpdateEvent(volume.Id, MessageFactoryEntityTypes.Volume),
                    false, ct);
            }
        }
    }

    public async Task SetUserCoverByUrl(int userId, string url, bool fromBase64 = true, bool chooseBetterImage = false,
        CancellationToken ct = default)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.UserPreferences, ct);
        if (user == null) return;

        await SetUserCoverByUrl(user, url, fromBase64, chooseBetterImage, ct);
    }

    public async Task SetUserCoverByUrl(AppUser user, string url, bool fromBase64 = true,
        bool chooseBetterImage = false, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(url))
        {
            var sourcePath = await StageCoverSource(url, fromBase64, ct);
            try
            {
                user.CoverImage = await WriteCover(sourcePath, ImageKind.UserCover, ImageService.GetUserFormat(user.Id) + ".webp", ct);
            }
            finally
            {
                _directoryService.DeleteFiles([sourcePath]);
            }

            _imageService.UpdateColorScape(user);
            _unitOfWork.UserRepository.Update(user);
        }
        else
        {
            user.CoverImage = string.Empty;
            _imageService.UpdateColorScape(user);
            _unitOfWork.UserRepository.Update(user);
        }

        if (_unitOfWork.HasChanges())
        {
            await _unitOfWork.CommitAsync(ct);
            await _eventHub.SendMessageAsync(MessageFactory.CoverUpdate,
                MessageFactory.CoverUpdateEvent(user.Id, MessageFactoryEntityTypes.User), false, ct);
        }
    }

    /// <summary>
    /// Deletes the cover a new write replaced. When it had the same name as the new file, the write already
    /// replaced it in place, and deleting it would remove the new cover
    /// </summary>
    private void DeleteReplacedCover(string? staleCover, string targetPath)
    {
        if (staleCover == null) return;
        if (string.Equals(Path.GetFullPath(staleCover), Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase)) return;

        // This will fail if Cover gen is done just before this as there is a bug with files getting locked
        _directoryService.DeleteFiles([staleCover]);
    }
}
