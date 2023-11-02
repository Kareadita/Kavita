using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.Recommendation;
using API.DTOs.Scrobbling;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers.Builders;
using Flurl.Http;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Kavita.Common.Helpers;
using Microsoft.Extensions.Logging;

namespace API.Services.Plus;
#nullable enable

public record PlusSeriesDto
{
    public int? AniListId { get; set; }
    public long? MalId { get; set; }
    public string? GoogleBooksId { get; set; }
    public string SeriesName { get; set; }
    public string? AltSeriesName { get; set; }
    public MediaFormat MediaFormat { get; set; }
    /// <summary>
    /// Optional but can help with matching
    /// </summary>
    public int? ChapterCount { get; set; }
    /// <summary>
    /// Optional but can help with matching
    /// </summary>
    public int? VolumeCount { get; set; }
    public int? Year { get; set; }
}

internal record MediaRecommendationDto
{
    public int Rating { get; set; }
    public IEnumerable<string> RecommendationNames { get; set; } = null!;
    public string Name { get; set; }
    public string CoverUrl { get; set; }
    public string SiteUrl { get; set; }
    public string? Summary { get; set; }
    public int? AniListId { get; set; }
    public long? MalId { get; set; }
}

public interface IRecommendationService
{
    Task<RecommendationDto> GetRecommendationsForSeries(int userId, int seriesId);
}

public class RecommendationService : IRecommendationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RecommendationService> _logger;

    public RecommendationService(IUnitOfWork unitOfWork, ILogger<RecommendationService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;

        FlurlHttp.ConfigureClient(Configuration.KavitaPlusApiUrl, cli =>
            cli.Settings.HttpClientFactory = new UntrustedCertClientFactory());
    }

    public async Task<RecommendationDto> GetRecommendationsForSeries(int userId, int seriesId)
    {
        var series =
            await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId,
                SeriesIncludes.Metadata | SeriesIncludes.Library | SeriesIncludes.Volumes | SeriesIncludes.Chapters);
        if (series == null || series.Library.Type == LibraryType.Comic) return new RecommendationDto();
        var license = await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey);

        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);
        var canSeeExternalSeries = user is {AgeRestriction: AgeRating.NotApplicable} &&
                                   await _unitOfWork.UserRepository.IsUserAdminAsync(user);

        var recDto = new RecommendationDto()
        {
            ExternalSeries = new List<ExternalSeriesDto>(),
            OwnedSeries = new List<SeriesDto>()
        };

        var recs = await GetRecommendations(license.Value, series);
        foreach (var rec in recs)
        {
            // Find the series based on name and type and that the user has access too
            var seriesForRec = await _unitOfWork.SeriesRepository.GetSeriesDtoByNamesAndMetadataIdsForUser(userId, rec.RecommendationNames,
                series.Library.Type, ScrobblingService.CreateUrl(ScrobblingService.AniListWeblinkWebsite, rec.AniListId),
                ScrobblingService.CreateUrl(ScrobblingService.MalWeblinkWebsite, rec.MalId));

            if (seriesForRec != null)
            {
                recDto.OwnedSeries.Add(seriesForRec);
                continue;
            }

            if (!canSeeExternalSeries) continue;
            // We can show this based on user permissions
            if (string.IsNullOrEmpty(rec.Name) || string.IsNullOrEmpty(rec.SiteUrl) || string.IsNullOrEmpty(rec.CoverUrl)) continue;
            recDto.ExternalSeries.Add(new ExternalSeriesDto()
            {
                Name = string.IsNullOrEmpty(rec.Name) ? rec.RecommendationNames.First() : rec.Name,
                Url = rec.SiteUrl,
                CoverUrl = rec.CoverUrl,
                Summary = rec.Summary,
                AniListId = rec.AniListId,
                MalId = rec.MalId
            });
        }

        await _unitOfWork.SeriesRepository.AddSeriesModifiers(userId, recDto.OwnedSeries);

        recDto.OwnedSeries = recDto.OwnedSeries.DistinctBy(s => s.Id).OrderBy(r => r.Name).ToList();
        recDto.ExternalSeries = recDto.ExternalSeries.DistinctBy(s => s.Name.ToNormalized()).OrderBy(r => r.Name).ToList();

        return recDto;
    }


    private async Task<IEnumerable<MediaRecommendationDto>> GetRecommendations(string license, Series series)
    {
        try
        {
            return await (Configuration.KavitaPlusApiUrl + "/api/recommendation")
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .WithHeader("x-license-key", license)
                .WithHeader("x-installId", HashUtil.ServerToken())
                .WithHeader("x-kavita-version", BuildInfo.Version)
                .WithHeader("Content-Type", "application/json")
                .WithTimeout(TimeSpan.FromSeconds(Configuration.DefaultTimeOutSecs))
                .PostJsonAsync(new PlusSeriesDtoBuilder(series).Build())
                .ReceiveJson<IEnumerable<MediaRecommendationDto>>();

        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error happened during the request to Kavita+ API");
        }

        return new List<MediaRecommendationDto>();
    }
}
