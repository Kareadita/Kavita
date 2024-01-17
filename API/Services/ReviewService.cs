using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Data.Repositories;
using API.DTOs.SeriesDetail;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using API.Helpers.Builders;
using API.Services.Plus;
using EasyCaching.Core;
using Flurl.Http;
using HtmlAgilityPack;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Kavita.Common.Helpers;
using Microsoft.Extensions.Logging;

namespace API.Services;

internal class MediaReviewDto
{
    public string Body { get; set; }
    public string Tagline { get; set; }
    public int Rating { get; set; }
    public int TotalVotes { get; set; }
    /// <summary>
    /// The media's overall Score
    /// </summary>
    public int Score { get; set; }
    public string SiteUrl { get; set; }
    /// <summary>
    /// In Markdown
    /// </summary>
    public string RawBody { get; set; }
    public string Username { get; set; }
    public ScrobbleProvider Provider { get; set; }
}

public interface IReviewService
{
    Task<IEnumerable<UserReviewDto>> GetReviewsForSeries(int userId, int seriesId);
}

public class ReviewService : IReviewService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ReviewService> _logger;
    private readonly ILicenseService _licenseService;
    private readonly IEasyCachingProvider _cacheProvider;
    public const string CacheKey = "review_";


    public ReviewService(IUnitOfWork unitOfWork, ILogger<ReviewService> logger, ILicenseService licenseService,
        IEasyCachingProviderFactory cachingProviderFactory)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _licenseService = licenseService;

        FlurlHttp.ConfigureClient(Configuration.KavitaPlusApiUrl, cli =>
            cli.Settings.HttpClientFactory = new UntrustedCertClientFactory());

        _cacheProvider = cachingProviderFactory.GetCachingProvider(EasyCacheProfiles.KavitaPlusReviews);
    }

    public async Task<IEnumerable<UserReviewDto>> GetReviewsForSeries(int userId, int seriesId)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);
        if (user == null) return ImmutableList<UserReviewDto>.Empty;
        var userRatings = (await _unitOfWork.UserRepository.GetUserRatingDtosForSeriesAsync(seriesId, userId))
            .Where(r => !string.IsNullOrEmpty(r.Body))
            .OrderByDescending(review => review.Username.Equals(user.UserName) ? 1 : 0)
            .ToList();

        if (!await _licenseService.HasActiveLicense())
        {
            return userRatings;
        }

        var cacheKey = CacheKey + seriesId;
        IList<UserReviewDto> externalReviews;

        var result = await _cacheProvider.GetAsync<IEnumerable<UserReviewDto>>(cacheKey);
        if (result.HasValue)
        {
            externalReviews = result.Value.ToList();
        }
        else
        {
            var reviews = (await GetExternalReviews(userId, seriesId)).ToList();
            externalReviews = SelectSpectrumOfReviews(reviews);

            await _cacheProvider.SetAsync(cacheKey, externalReviews, TimeSpan.FromHours(10));
            _logger.LogDebug("Caching external reviews for {Key}", cacheKey);
        }


        // Fetch external reviews and splice them in
        userRatings.AddRange(externalReviews);

        return userRatings;
    }

    private static IList<UserReviewDto> SelectSpectrumOfReviews(IList<UserReviewDto> reviews)
    {
        IList<UserReviewDto> externalReviews;
        var totalReviews = reviews.Count;

        if (totalReviews > 10)
        {
            var stepSize = Math.Max((totalReviews - 4) / 8, 1);

            var selectedReviews = new List<UserReviewDto>()
            {
                reviews[0],
                reviews[1],
            };
            for (var i = 2; i < totalReviews - 2; i += stepSize)
            {
                selectedReviews.Add(reviews[i]);

                if (selectedReviews.Count >= 8)
                    break;
            }

            selectedReviews.Add(reviews[totalReviews - 2]);
            selectedReviews.Add(reviews[totalReviews - 1]);

            externalReviews = selectedReviews;
        }
        else
        {
            externalReviews = reviews;
        }

        return externalReviews;
    }

    private async Task<IEnumerable<UserReviewDto>> GetExternalReviews(int userId, int seriesId)
    {
        var series =
            await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId,
                SeriesIncludes.Metadata | SeriesIncludes.Library | SeriesIncludes.Chapters | SeriesIncludes.Volumes);
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);
        if (user == null || series == null) return new List<UserReviewDto>();
        var license = await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey);
        var ret = (await GetReviews(license.Value, series)).Select(r => new UserReviewDto()
        {
            Body = r.Body,
            Tagline = r.Tagline,
            Score = r.Score,
            Username = r.Username,
            LibraryId = series.LibraryId,
            SeriesId = series.Id,
            IsExternal = true,
            Provider = r.Provider,
            BodyJustText = GetCharacters(r.Body),
            ExternalUrl = r.SiteUrl
        });

        return ret.OrderByDescending(r => r.Score);
    }

    private static string GetCharacters(string body)
    {
        if (string.IsNullOrEmpty(body)) return body;

        var doc = new HtmlDocument();
        doc.LoadHtml(body);

        var textNodes = doc.DocumentNode.SelectNodes("//text()[not(parent::script)]");
        if (textNodes == null) return string.Empty;
        var plainText =  string.Join(" ", textNodes
            .Select(node => node.InnerText)
            .Where(s => !s.Equals("\n")));

        // Clean any leftover markdown out
        plainText = Regex.Replace(plainText, @"[_*\[\]~]", string.Empty);
        plainText = Regex.Replace(plainText, @"img\d*\((.*?)\)", string.Empty);
        plainText = Regex.Replace(plainText, @"~~~(.*?)~~~", "$1");
        plainText = Regex.Replace(plainText, @"\+{3}(.*?)\+{3}", "$1");
        plainText = Regex.Replace(plainText, @"~~(.*?)~~", "$1");
        plainText = Regex.Replace(plainText, @"__(.*?)__", "$1");
        plainText = Regex.Replace(plainText, @"#\s(.*?)", "$1");

        // Just strip symbols
        plainText = Regex.Replace(plainText, @"[_*\[\]~]", string.Empty);
        plainText = Regex.Replace(plainText, @"img\d*\((.*?)\)", string.Empty);
        plainText = Regex.Replace(plainText, @"~~~", string.Empty);
        plainText = Regex.Replace(plainText, @"\+", string.Empty);
        plainText = Regex.Replace(plainText, @"~~", string.Empty);
        plainText = Regex.Replace(plainText, @"__", string.Empty);

        // Take the first 100 characters
        plainText = plainText.Length > 100 ? plainText.Substring(0, 100) : plainText;

        return plainText + "…";
    }


    private async Task<IEnumerable<MediaReviewDto>> GetReviews(string license, Series series)
    {
        _logger.LogDebug("Fetching external reviews for Series: {SeriesName}", series.Name);
        try
        {
            return await (Configuration.KavitaPlusApiUrl + "/api/review")
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .WithHeader("x-license-key", license)
                .WithHeader("x-installId", HashUtil.ServerToken())
                .WithHeader("x-kavita-version", BuildInfo.Version)
                .WithHeader("Content-Type", "application/json")
                .WithTimeout(TimeSpan.FromSeconds(Configuration.DefaultTimeOutSecs))
                .PostJsonAsync(new PlusSeriesDtoBuilder(series).Build())
                .ReceiveJson<IEnumerable<MediaReviewDto>>();

        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error happened during the request to Kavita+ API");
        }

        return new List<MediaReviewDto>();
    }
}
