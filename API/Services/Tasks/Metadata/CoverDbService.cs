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
using API.Entities.Person;
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
    Task SetPersonCoverByUrl(Person person, string url, bool fromBase64 = true, bool checkNoImagePlaceholder = false, bool chooseBetterImage = true);
    Task SetSeriesCoverByUrl(Series series, string url, bool fromBase64 = true, bool chooseBetterImage = false);
    Task SetChapterCoverByUrl(Chapter chapter, string url, bool fromBase64 = true, bool chooseBetterImage = false);
    Task SetUserCoverByUrl(AppUser user, string url, bool fromBase64 = true, bool chooseBetterImage = false);
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
    private TimeSpan _cacheTime = TimeSpan.FromDays(10);

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

    /// <summary>
    /// Downloads the favicon image from a given website URL, optionally falling back to a custom method if standard methods fail.
    /// </summary>
    /// <param name="url">The full URL of the website to extract the favicon from.</param>
    /// <param name="encodeFormat">The desired image encoding format for saving the favicon (e.g., WebP, PNG).</param>
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

        await provider.SetAsync(baseUrl, string.Empty, _cacheTime);
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

            image.WriteToFile(Path.Combine(_directoryService.FaviconDirectory, filename));
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
            // Sanitize user input
            publisherName = publisherName.Replace(Environment.NewLine, string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);
            var provider = _cacheFactory.GetCachingProvider(EasyCacheProfiles.Publisher);
            var res = await provider.GetAsync<string>(publisherName);
            if (res.HasValue)
            {
                _logger.LogInformation("Kavita has already tried to fetch Publisher: {PublisherName} and failed. Skipping duplicate check", publisherName);
                throw new KavitaException($"Kavita has already tried to fetch Publisher: {publisherName} and failed. Skipping duplicate check");
            }

            await provider.SetAsync(publisherName, string.Empty, _cacheTime);
            var publisherLink = await FallbackToKavitaReaderPublisher(publisherName);
            if (string.IsNullOrEmpty(publisherLink))
            {
                throw new KavitaException($"Could not grab publisher image for {publisherName}");
            }

            // Create the destination file path
            var filename = ImageService.GetPublisherFormat(publisherName, encodeFormat);

            _logger.LogTrace("Fetching publisher image from {Url}", publisherLink.Sanitize());
            await DownloadImageFromUrl(publisherName, encodeFormat, publisherLink, _directoryService.PublisherDirectory);

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

    private async Task<string> DownloadImageFromUrl(string filenameWithoutExtension, EncodeFormat encodeFormat, string url, string? targetDirectory = null)
    {
        // TODO: I need to unit test this to ensure it works when overwriting, etc

        // Target Directory defaults to CoverImageDirectory, but can be temp for when comparison between images is used
        targetDirectory ??= _directoryService.CoverImageDirectory;

        // Create the destination file path
        var filename = filenameWithoutExtension + encodeFormat.GetExtension();
        var targetFile = Path.Combine(targetDirectory, filename);

        _logger.LogTrace("Fetching person image from {Url}", url.Sanitize());
        // Download the file using Flurl
        var imageStream = await url
            .AllowHttpStatus("2xx,304")
            .GetStreamAsync();

        using var image = Image.NewFromStream(imageStream);
        try
        {
            image.WriteToFile(targetFile);
        }
        catch (Exception ex)
        {
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
        }

        return filename;
    }

    private async Task<string?> GetCoverPersonImagePath(Person person)
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
    public async Task SetPersonCoverByUrl(Person person, string url, bool fromBase64 = true, bool checkNoImagePlaceholder = false, bool chooseBetterImage = true)
    {
        if (!string.IsNullOrEmpty(url))
        {
            var tempDir = _directoryService.TempDirectory;
            var format = ImageService.GetPersonFormat(person.Id);
            var finalFileName = format + ".webp";
            var tempFileName = format + "_new";

            // This is writing the image to CoverDirectory
            var tempFilePath = await CreateThumbnail(url, tempFileName, fromBase64, tempDir);

            if (!string.IsNullOrEmpty(tempFilePath))
            {
                var tempFullPath = Path.Combine(tempDir, tempFilePath);
                var finalFullPath = Path.Combine(_directoryService.CoverImageDirectory, finalFileName);

                // Skip setting image if it's similar to a known placeholder
                if (checkNoImagePlaceholder)
                {
                    var placeholderPath = Path.Combine(_directoryService.AssetsDirectory, "anilist-no-image-placeholder.jpg");
                    var similarity = placeholderPath.CalculateSimilarity(tempFullPath);
                    if (similarity >= 0.9f)
                    {
                        _logger.LogInformation("Skipped setting placeholder image for person {PersonId} due to high similarity ({Similarity})", person.Id, similarity);
                        _directoryService.DeleteFiles([tempFullPath]);
                        return;
                    }
                }

                try
                {
                    if (!string.IsNullOrEmpty(person.CoverImage) && chooseBetterImage)
                    {
                        var existingPath = Path.Combine(_directoryService.CoverImageDirectory, person.CoverImage);
                        var betterImage = existingPath.GetBetterImage(tempFullPath)!;

                        var choseNewImage = string.Equals(betterImage, tempFullPath, StringComparison.OrdinalIgnoreCase);
                        if (choseNewImage)
                        {
                            _directoryService.DeleteFiles([existingPath]);
                            _directoryService.CopyFile(tempFullPath, finalFullPath);
                            person.CoverImage = finalFileName;
                        }
                        else
                        {
                            _directoryService.DeleteFiles([tempFullPath]);
                            return;
                        }
                    }
                    else
                    {
                        _directoryService.CopyFile(tempFullPath, finalFullPath);
                        person.CoverImage = finalFileName;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error choosing better image for Person: {PersonId}", person.Id);
                    _directoryService.CopyFile(tempFullPath, finalFullPath);
                    person.CoverImage = finalFileName;
                }

                _directoryService.DeleteFiles([tempFullPath]);

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
            var tempDir = _directoryService.TempDirectory;
            var format = ImageService.GetSeriesFormat(series.Id);
            var finalFileName = format + ".webp";
            var tempFileName = format + "_new";
            var tempFilePath = await CreateThumbnail(url, tempFileName, fromBase64, tempDir);

            if (!string.IsNullOrEmpty(tempFilePath))
            {
                var tempFullPath = Path.Combine(tempDir, tempFilePath);
                var finalFullPath = Path.Combine(_directoryService.CoverImageDirectory, finalFileName);

                if (chooseBetterImage && !string.IsNullOrEmpty(series.CoverImage))
                {
                    try
                    {
                        var existingPath = Path.Combine(_directoryService.CoverImageDirectory, series.CoverImage);
                        var betterImage = existingPath.GetBetterImage(tempFullPath)!;

                        var choseNewImage = string.Equals(betterImage, tempFullPath, StringComparison.OrdinalIgnoreCase);
                        if (choseNewImage)
                        {
                            // Don't delete the Series cover unless it is an override, otherwise the first chapter will be null
                            if (existingPath.Contains(ImageService.GetSeriesFormat(series.Id)))
                            {
                                _directoryService.DeleteFiles([existingPath]);
                            }

                            _directoryService.CopyFile(tempFullPath, finalFullPath);
                            series.CoverImage = finalFileName;
                        }
                        else
                        {
                            _directoryService.DeleteFiles([tempFullPath]);
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error choosing better image for Series: {SeriesId}", series.Id);
                        _directoryService.CopyFile(tempFullPath, finalFullPath);
                        series.CoverImage = finalFileName;
                    }
                }
                else
                {
                    _directoryService.CopyFile(tempFullPath, finalFullPath);
                    series.CoverImage = finalFileName;
                }

                _directoryService.DeleteFiles([tempFullPath]);
                series.CoverImageLocked = true;
                _imageService.UpdateColorScape(series);
                _unitOfWork.SeriesRepository.Update(series);
            }
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
            await _unitOfWork.CommitAsync();
            await _eventHub.SendMessageAsync(MessageFactory.CoverUpdate,
                MessageFactory.CoverUpdateEvent(series.Id, MessageFactoryEntityTypes.Series), false);
        }
    }

    // TODO: Refactor this to IHasCoverImage instead of a hard entity type
    public async Task SetChapterCoverByUrl(Chapter chapter, string url, bool fromBase64 = true, bool chooseBetterImage = false)
    {
        if (!string.IsNullOrEmpty(url))
        {
            var tempDirectory = _directoryService.TempDirectory;
            var finalFileName = ImageService.GetChapterFormat(chapter.Id, chapter.VolumeId) + ".webp";
            var tempFileName = ImageService.GetChapterFormat(chapter.Id, chapter.VolumeId) + "_new";

            var tempFilePath = await CreateThumbnail(url, tempFileName, fromBase64, tempDirectory);

            if (!string.IsNullOrEmpty(tempFilePath))
            {
                var tempFullPath = Path.Combine(tempDirectory, tempFilePath);
                var finalFullPath = Path.Combine(_directoryService.CoverImageDirectory, finalFileName);

                if (chooseBetterImage && !string.IsNullOrEmpty(chapter.CoverImage))
                {
                    try
                    {
                        var existingPath = Path.Combine(_directoryService.CoverImageDirectory, chapter.CoverImage);
                        var betterImage = existingPath.GetBetterImage(tempFullPath)!;
                        var choseNewImage = string.Equals(betterImage, tempFullPath, StringComparison.OrdinalIgnoreCase);

                        if (choseNewImage)
                        {
                            // This will fail if Cover gen is done just before this as there is a bug with files getting locked.
                            _directoryService.DeleteFiles([existingPath]);
                            _directoryService.CopyFile(tempFullPath, finalFullPath);
                            _directoryService.DeleteFiles([tempFullPath]);
                        }
                        else
                        {
                            _directoryService.DeleteFiles([tempFullPath]);
                            return;
                        }

                        chapter.CoverImage = finalFileName;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "There was an issue trying to choose a better cover image for Chapter: {FileName} ({ChapterId})", chapter.Range, chapter.Id);
                    }
                }
                else
                {
                    // No comparison needed, just copy and rename to final
                    _directoryService.CopyFile(tempFullPath, finalFullPath);
                    _directoryService.DeleteFiles([tempFullPath]);
                    chapter.CoverImage = finalFileName;
                }

                chapter.CoverImageLocked = true;
                _imageService.UpdateColorScape(chapter);
                _unitOfWork.ChapterRepository.Update(chapter);
            }
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
            await _unitOfWork.CommitAsync();
            await _eventHub.SendMessageAsync(
                MessageFactory.CoverUpdate,
                MessageFactory.CoverUpdateEvent(chapter.Id, MessageFactoryEntityTypes.Chapter),
                false
            );
        }
    }

    public async Task SetUserCoverByUrl(AppUser user, string url, bool fromBase64 = true, bool chooseBetterImage = false)
    {
        if (!string.IsNullOrEmpty(url))
        {
            var tempDir = _directoryService.TempDirectory;
            var finalFileName = ImageService.GetUserFormat(user.Id) + ".webp";
            var tempFileName = ImageService.GetUserFormat(user.Id) + "_new";
            var finalFullPath = Path.Combine(_directoryService.CoverImageDirectory, finalFileName);

            // This is writing the image to CoverDirectory
            var tempFilePath = await CreateThumbnail(url, tempFileName, fromBase64, tempDir);

            if (!string.IsNullOrEmpty(tempFilePath))
            {
                var tempFullPath = Path.Combine(tempDir, tempFilePath);


                _directoryService.CopyFile(tempFullPath, finalFullPath);
                _directoryService.DeleteFiles([tempFullPath]);

                user.CoverImage = finalFileName;
                _unitOfWork.UserRepository.Update(user);
                _imageService.UpdateColorScape(user);
                _unitOfWork.UserRepository.Update(user);
            }
        }
        else
        {
            user.CoverImage = string.Empty;
            _imageService.UpdateColorScape(user);
            _unitOfWork.UserRepository.Update(user);
        }

        if (_unitOfWork.HasChanges())
        {
            await _unitOfWork.CommitAsync();
            await _eventHub.SendMessageAsync(MessageFactory.CoverUpdate,
                MessageFactory.CoverUpdateEvent(user.Id, MessageFactoryEntityTypes.User), false);
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="url"></param>
    /// <param name="filenameWithoutExtension">Filename without extension</param>
    /// <param name="fromBase64"></param>
    /// <param name="targetDirectory">Allows a different directory to be written to</param>
    /// <returns></returns>
    private async Task<string> CreateThumbnail(string url, string filenameWithoutExtension, bool fromBase64 = true, string? targetDirectory = null)
    {
        targetDirectory ??= _directoryService.CoverImageDirectory;

        var settings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        var encodeFormat = settings.EncodeMediaAs;
        var coverImageSize = settings.CoverImageSize;

        if (fromBase64)
        {
            return _imageService.CreateThumbnailFromBase64(url,
                filenameWithoutExtension, encodeFormat, coverImageSize.GetDimensions().Width, targetDirectory);
        }

        return await DownloadImageFromUrl(filenameWithoutExtension, encodeFormat, url, targetDirectory);
    }
}
