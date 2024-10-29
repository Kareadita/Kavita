using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Entities;
using API.Entities.Enums;
using EasyCaching.Core;
using Flurl;
using Flurl.Http;
using HtmlAgilityPack;
using Kavita.Common;
using Microsoft.Extensions.Logging;
using NetVips;

namespace API.Services.Tasks.Metadata;

public interface ICoverDbService
{
    Task<string> DownloadFaviconAsync(string url, EncodeFormat encodeFormat);
    Task<string> DownloadPublisherImageAsync(string publisherName, EncodeFormat encodeFormat);
    Task<string> DownloadPersonImageAsync(Person person, EncodeFormat encodeFormat);
}


public class CoverDbService : ICoverDbService
{
    private readonly ILogger<CoverDbService> _logger;
    private readonly IDirectoryService _directoryService;
    private readonly IEasyCachingProviderFactory _cacheFactory;

    private static readonly string[] ValidIconRelations = {
        "icon",
        "apple-touch-icon",
        "apple-touch-icon-precomposed",
        "apple-touch-icon icon-precomposed" // ComicVine has it combined
    };

    /// <summary>
    /// A mapping of urls that need to get the icon from another url, due to strangeness (like app.plex.tv loading a black icon)
    /// </summary>
    private static readonly IDictionary<string, string> FaviconUrlMapper = new Dictionary<string, string>
    {
        ["https://app.plex.tv"] = "https://plex.tv"
    };

    public CoverDbService(ILogger<CoverDbService> logger, IDirectoryService directoryService, IEasyCachingProviderFactory cacheFactory)
    {
        _logger = logger;
        _directoryService = directoryService;
        _cacheFactory = cacheFactory;
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
            _logger.LogInformation("Kavita has already tried to fetch from {BaseUrl} and failed. Skipping duplicate check", baseUrl);
            throw new KavitaException($"Kavita has already tried to fetch from {baseUrl} and failed. Skipping duplicate check");
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

            var finalUrl = publisherLink;

            _logger.LogTrace("Fetching publisher image from {Url}", finalUrl);
            // Download the favicon.ico file using Flurl
            var publisherStream = await finalUrl
                .AllowHttpStatus("2xx,304")
                .GetStreamAsync();

            // Create the destination file path
            using var image = Image.PngloadStream(publisherStream);
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


            _logger.LogDebug("Publisher image for {PublisherName} downloaded and saved successfully", publisherName);
            return filename;
        } catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading image for {PublisherName}", publisherName);
            throw;
        }
    }

    /// <summary>
    /// Attempts to download the Person image from CoverDB while matching against metadata within the Person
    /// </summary>
    /// <param name="person"></param>
    /// <param name="encodeFormat"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public Task<string> DownloadPersonImageAsync(Person person, EncodeFormat encodeFormat)
    {
        throw new NotImplementedException();
    }

    private static async Task<string> FallbackToKavitaReaderFavicon(string baseUrl)
    {
        var correctSizeLink = string.Empty;
        var allOverrides = await "https://www.kavitareader.com/assets/favicons/urls.txt".GetStringAsync();
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
                throw new KavitaException($"Could not grab favicon from {baseUrl}");
            }

            correctSizeLink = "https://www.kavitareader.com/assets/favicons/" + externalFile;
        }

        return correctSizeLink;
    }

    private static async Task<string> FallbackToKavitaReaderPublisher(string publisherName)
    {
        var externalLink = string.Empty;
        var allOverrides = await "https://www.kavitareader.com/assets/publishers/publishers.txt".GetStringAsync();
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

            externalLink = "https://www.kavitareader.com/assets/publishers/" + externalFile;
        }

        return externalLink;
    }
}
