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
using Kavita.Models.Entities.Interfaces;
using Kavita.Models.Entities.Person;
using Kavita.Models.Entities.User;
using Kavita.Models.Images;
using Kavita.Services.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Kavita.Services.Helpers;

namespace Kavita.Services.Metadata;

public class CoverDbService : ICoverDbService
{
    private readonly ILogger<CoverDbService> _logger;
    private readonly IDirectoryService _directoryService;
    private readonly IEasyCachingProviderFactory _cacheFactory;
    private readonly IHostEnvironment _env;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IImageFileService _imageFileService;
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
        IEasyCachingProviderFactory cacheFactory, IHostEnvironment env, IUnitOfWork unitOfWork,
        IUrlValidationService urlValidationService, IImageFileService imageFileService)
    {
        _imageFileService = imageFileService;
        _logger = logger;
        _directoryService = directoryService;
        _cacheFactory = cacheFactory;
        _env = env;
        _unitOfWork = unitOfWork;
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
            var origin = ImageOrigin.Website;
            if (string.IsNullOrEmpty(correctSizeLink))
            {
                correctSizeLink = await FallbackToKavitaReaderFavicon(baseUrl);
                origin = ImageOrigin.CoversDb;
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
            var filename = await DownloadKeyedAsync(finalUrl, ImageKind.Favicon, domain,
                ImageHelper.GetWebLinkFormat(baseUrl, encodeFormat), origin, ct);
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
            var filename = await DownloadKeyedAsync(publisherLink, ImageKind.Publisher, publisherName,
                ImageHelper.GetPublisherFormat(publisherName, encodeFormat), ImageOrigin.CoversDb, ct);

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

    public async Task SetCoverByUrlAsync(IHasCoverImageFile entity, string url, CoverWrite write, CancellationToken ct = default)
    {
        var sourcePath = await DownloadImageFromUrl(url, ct);
        try
        {
            await _imageFileService.SetCoverAsync(entity, new ImageSource.FromFile(sourcePath), write, ct: ct);
        }
        finally
        {
            _directoryService.DeleteFiles([sourcePath]);
        }
    }

    public async Task SetOidcUserCoverAsync(int userId, string url, CancellationToken ct = default)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.CoverImageFile, ct);
        if (user == null) return;

        await SetCoverByUrlAsync(user, url, new CoverWrite(ImageOrigin.Oidc, Lock: false), ct);
    }

    private async Task<string> DownloadKeyedAsync(string url, ImageKind kind, string key, string fileName,
        ImageOrigin origin, CancellationToken ct)
    {
        var sourcePath = await DownloadImageFromUrl(url, ct);
        try
        {
            return await _imageFileService.WriteKeyedAsync(new ImageSource.FromFile(sourcePath), kind, key, fileName, origin, ct);
        }
        finally
        {
            _directoryService.DeleteFiles([sourcePath]);
        }
    }
}
