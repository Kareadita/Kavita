using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Data.Repositories;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.SignalR;
using EasyCaching.Core;
using Flurl;
using Flurl.Http;
using HtmlAgilityPack;
using Kavita.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetVips;


namespace API.Services.Tasks.Metadata;
#nullable enable

public interface ICoverDbService
{
    Task<string> DownloadFaviconAsync(string url, EncodeFormat encodeFormat);
    Task<string> DownloadPublisherImageAsync(string publisherName, EncodeFormat encodeFormat);
    Task<string?> DownloadPersonImageAsync(Person person, EncodeFormat encodeFormat);
    Task<string?> DownloadPersonImageAsync(Person person, EncodeFormat encodeFormat, string url);
    Task SetPersonCoverByUrl(Person person, string url, bool fromBase64 = true, bool checkNoImagePlaceholder = false);
    Task SetSeriesCoverByUrl(Series series, string url, bool fromBase64 = true, bool chooseBetterImage = false);
}


public class CoverDbService : ICoverDbService
{
    private readonly ILogger<CoverDbService> _logger;
    private readonly IDirectoryService _directoryService;
    private readonly IEasyCachingProviderFactory _cacheFactory;
    private readonly IHostEnvironment _env;
    private readonly IImageService _imageService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventHub _eventHub;

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

    public CoverDbService(ILogger<CoverDbService> logger, IDirectoryService directoryService,
        IEasyCachingProviderFactory cacheFactory, IHostEnvironment env, IImageService imageService,
        IUnitOfWork unitOfWork, IEventHub eventHub)
    {
        _logger = logger;
        _directoryService = directoryService;
        _cacheFactory = cacheFactory;
        _env = env;
        _imageService = imageService;
        _unitOfWork = unitOfWork;
        _eventHub = eventHub;
    }

    public async Task<string> DownloadFaviconAsync(string url, EncodeFormat encodeFormat)
    {
        // Parse the URL to get the domain (including subdomain)
        var uri = new Uri(url);
        var domain = uri.Host.Replace(Environment.NewLine, string.Empty);
        var baseUrl = uri.Scheme + "://" + uri.Host;


        var provider = _cacheFactory.GetCachingProvider(EasyCacheProfiles.Favicon);
        var res = await provider.GetAsync<string>(baseUrl);
        if (res.HasValue)
        {
            var sanitizedBaseUrl = baseUrl.Sanitize();
            _logger.LogInformation("Kavita has already tried to fetch from {BaseUrl} and failed. Skipping duplicate check", sanitizedBaseUrl);
            throw new KavitaException($"Kavita has already tried to fetch from {sanitizedBaseUrl} and failed. Skipping duplicate check");
        }

        await provider.SetAsync(baseUrl, string.Empty, TimeSpan.FromDays(10));
        if (FaviconUrlMapper.TryGetValue(baseUrl, out var value))
        {
            url = value;
        }

        var correctSizeLink = string.Empty;

        try
        {
            var htmlContent = url.GetStringAsync().Result;
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
            // Download the favicon.ico file using Flurl
            var faviconStream = await finalUrl
                .AllowHttpStatus("2xx,304")
                .GetStreamAsync();

            // Create the destination file path
            using var image = Image.PngloadStream(faviconStream);
            var filename = ImageService.GetWebLinkFormat(baseUrl, encodeFormat);
            switch (encodeFormat)
            {
                case EncodeFormat.PNG:
                    image.Pngsave(Path.Combine(_directoryService.FaviconDirectory, filename));
                    break;
                case EncodeFormat.WEBP:
                    image.Webpsave(Path.Combine(_directoryService.FaviconDirectory, filename));
                    break;
                case EncodeFormat.AVIF:
                    image.Heifsave(Path.Combine(_directoryService.FaviconDirectory, filename));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(encodeFormat), encodeFormat, null);
            }


            _logger.LogDebug("Favicon for {Domain} downloaded and saved successfully", domain);
            return filename;
        } catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading favicon for {Domain}", domain);
            throw;
        }
    }

    public async Task<string> DownloadPublisherImageAsync(string publisherName, EncodeFormat encodeFormat)
    {
        try
        {
            var publisherLink = await FallbackToKavitaReaderPublisher(publisherName);
            if (string.IsNullOrEmpty(publisherLink))
            {
                throw new KavitaException($"Could not grab publisher image for {publisherName}");
            }

            _logger.LogTrace("Fetching publisher image from {Url}", publisherLink.Sanitize());
            // Download the publisher file using Flurl
            var publisherStream = await publisherLink
                .AllowHttpStatus("2xx,304")
                .GetStreamAsync();

            // Create the destination file path
            using var image = Image.NewFromStream(publisherStream);
            var filename = ImageService.GetPublisherFormat(publisherName, encodeFormat);
            switch (encodeFormat)
            {
                case EncodeFormat.PNG:
                    image.Pngsave(Path.Combine(_directoryService.PublisherDirectory, filename));
                    break;
                case EncodeFormat.WEBP:
                    image.Webpsave(Path.Combine(_directoryService.PublisherDirectory, filename));
                    break;
                case EncodeFormat.AVIF:
                    image.Heifsave(Path.Combine(_directoryService.PublisherDirectory, filename));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(encodeFormat), encodeFormat, null);
            }


            _logger.LogDebug("Publisher image for {PublisherName} downloaded and saved successfully", publisherName.Sanitize());
            return filename;
        } catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading image for {PublisherName}", publisherName.Sanitize());
            throw;
        }
    }

    /// <summary>
    /// Attempts to download the Person image from CoverDB while matching against metadata within the Person
    /// </summary>
    /// <param name="person"></param>
    /// <param name="encodeFormat"></param>
    /// <returns>Person image (in correct directory) or null if not found/error</returns>
    public async Task<string?> DownloadPersonImageAsync(Person person, EncodeFormat encodeFormat)
    {
        try
        {
            var personImageLink = await GetCoverPersonImagePath(person);
            if (string.IsNullOrEmpty(personImageLink))
            {
                throw new KavitaException($"Could not grab person image for {person.Name}");
            }
            return await DownloadPersonImageAsync(person, encodeFormat, personImageLink);
        } catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading image for {PersonName}", person.Name);
        }

        return null;
    }

    /// <summary>
    /// Attempts to download the Person cover image from a Url
    /// </summary>
    /// <param name="person"></param>
    /// <param name="encodeFormat"></param>
    /// <param name="url"></param>
    /// <returns></returns>
    /// <exception cref="KavitaException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public async Task<string?> DownloadPersonImageAsync(Person person, EncodeFormat encodeFormat, string url)
    {
        try
        {
            var personImageLink = await GetCoverPersonImagePath(person);
            if (string.IsNullOrEmpty(personImageLink))
            {
                throw new KavitaException($"Could not grab person image for {person.Name}");
            }


            var filename = await DownloadImageFromUrl(ImageService.GetPersonFormat(person.Id), encodeFormat, personImageLink);

            _logger.LogDebug("Person image for {PersonName} downloaded and saved successfully", person.Name);

            return filename;
        } catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading image for {PersonName}", person.Name);
        }

        return null;
    }

    private async Task<string> DownloadImageFromUrl(string filenameWithoutExtension, EncodeFormat encodeFormat, string url)
    {
        // Create the destination file path
        var filename = filenameWithoutExtension + encodeFormat.GetExtension();
        var targetFile = Path.Combine(_directoryService.CoverImageDirectory, filename);

        // Ensure if file exists, we delete to overwrite

        _logger.LogTrace("Fetching person image from {Url}", url.Sanitize());
        // Download the file using Flurl
        var personStream = await url
            .AllowHttpStatus("2xx,304")
            .GetStreamAsync();

        using var image = Image.NewFromStream(personStream);
        switch (encodeFormat)
        {
            case EncodeFormat.PNG:
                image.Pngsave(targetFile);
                break;
            case EncodeFormat.WEBP:
                image.Webpsave(targetFile);
                break;
            case EncodeFormat.AVIF:
                image.Heifsave(targetFile);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(encodeFormat), encodeFormat, null);
        }

        return filename;
    }

    private async Task<string> GetCoverPersonImagePath(Person person)
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
                .DownloadFileAsync(_directoryService.LongTermCacheDirectory);

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
        const string urlsFileName = "publishers.txt";
        var correctSizeLink = string.Empty;
        var allOverrides = await GetCachedData(urlsFileName) ??
                           await $"{NewHost}favicons/{urlsFileName}".GetStringAsync();

        // Cache immediately
        await CacheDataAsync(urlsFileName, allOverrides);


        if (!string.IsNullOrEmpty(allOverrides))
        {
            var cleanedBaseUrl = baseUrl.Replace("https://", string.Empty);
            var externalFile = allOverrides
                .Split("\n")
                .FirstOrDefault(url =>
                    cleanedBaseUrl.Equals(url.Replace(".png", string.Empty)) ||
                    cleanedBaseUrl.Replace("www.", string.Empty).Equals(url.Replace(".png", string.Empty)
                    ));

            if (string.IsNullOrEmpty(externalFile))
            {
                throw new KavitaException($"Could not grab favicon from {baseUrl.Sanitize()}");
            }

            correctSizeLink = $"{NewHost}favicons/" + externalFile;
        }

        return correctSizeLink;
    }

    private async Task<string> FallbackToKavitaReaderPublisher(string publisherName)
    {
        const string publisherFileName = "publishers.txt";
        var externalLink = string.Empty;
        var allOverrides = await GetCachedData(publisherFileName) ??
                           await $"{NewHost}publishers/{publisherFileName}".GetStringAsync();

        // Cache immediately
        await CacheDataAsync(publisherFileName, allOverrides);


        if (!string.IsNullOrEmpty(allOverrides))
        {
            var externalFile = allOverrides
                .Split("\n")
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

            externalLink = $"{NewHost}publishers/" + externalFile;
        }

        return externalLink;
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
    public async Task SetPersonCoverByUrl(Person person, string url, bool fromBase64 = true, bool checkNoImagePlaceholder = false)
    {
        // TODO: Refactor checkNoImagePlaceholder bool to an action that evaluates how to process Image
        if (!string.IsNullOrEmpty(url))
        {
            var filePath = await CreateThumbnail(url, $"{ImageService.GetPersonFormat(person.Id)}", fromBase64);

            // Additional check to see if downloaded image is similar and we have a higher resolution
            if (checkNoImagePlaceholder)
            {
                var matchRating = Path.Join(_directoryService.AssetsDirectory, "anilist-no-image-placeholder.jpg").GetSimilarity(Path.Join(_directoryService.CoverImageDirectory, filePath))!;

                if (matchRating >= 0.9f)
                {
                    if (string.IsNullOrEmpty(person.CoverImage))
                    {
                        filePath = null;
                    }
                    else
                    {
                        filePath = Path.GetFileName(Path.Join(_directoryService.CoverImageDirectory, person.CoverImage));
                    }

                }
            }

            if (!string.IsNullOrEmpty(filePath))
            {
                person.CoverImage = filePath;
                person.CoverImageLocked = true;
                _imageService.UpdateColorScape(person);
                _unitOfWork.PersonRepository.Update(person);
            }
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
            await _unitOfWork.CommitAsync();
            await _eventHub.SendMessageAsync(MessageFactory.CoverUpdate,
                MessageFactory.CoverUpdateEvent(person.Id, MessageFactoryEntityTypes.Person), false);
        }
    }

    /// <summary>
    /// Sets the series cover by url
    /// </summary>
    /// <param name="series"></param>
    /// <param name="url"></param>
    /// <param name="fromBase64"></param>
    /// <param name="chooseBetterImage">If images are similar, will choose the higher quality image</param>
    public async Task SetSeriesCoverByUrl(Series series, string url, bool fromBase64 = true, bool chooseBetterImage = false)
    {
        if (!string.IsNullOrEmpty(url))
        {
            var filePath = await CreateThumbnail(url, $"{ImageService.GetSeriesFormat(series.Id)}", fromBase64);

            if (!string.IsNullOrEmpty(filePath))
            {
                // Additional check to see if downloaded image is similar and we have a higher resolution
                if (chooseBetterImage)
                {
                    try
                    {
                        var betterImage = Path.Join(_directoryService.CoverImageDirectory, series.CoverImage)
                            .GetBetterImage(Path.Join(_directoryService.CoverImageDirectory, filePath))!;
                        filePath = Path.GetFileName(betterImage);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "There was an issue trying to choose a better cover image for Series: {SeriesName} ({SeriesId})", series.Name, series.Id);
                    }
                }

                series.CoverImage = filePath;
                series.CoverImageLocked = true;
                _imageService.UpdateColorScape(series);
                _unitOfWork.SeriesRepository.Update(series);
            }
        }
        else
        {
            series.CoverImage = string.Empty;
            series.CoverImageLocked = false;
            _imageService.UpdateColorScape(series);
            _unitOfWork.SeriesRepository.Update(series);
        }

        if (_unitOfWork.HasChanges())
        {
            await _unitOfWork.CommitAsync();
            await _eventHub.SendMessageAsync(MessageFactory.CoverUpdate,
                MessageFactory.CoverUpdateEvent(series.Id, MessageFactoryEntityTypes.Series), false);
        }
    }

    private async Task<string> CreateThumbnail(string url, string filename, bool fromBase64 = true)
    {
        var settings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        var encodeFormat = settings.EncodeMediaAs;
        var coverImageSize = settings.CoverImageSize;

        if (fromBase64)
        {
            return _imageService.CreateThumbnailFromBase64(url,
                filename, encodeFormat, coverImageSize.GetDimensions().Width);
        }

        return await DownloadImageFromUrl(filename, encodeFormat, url);
    }
}
