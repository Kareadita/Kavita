using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.Scrobbling;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using Flurl.Http;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Kavita.Common.Helpers;
using Microsoft.Extensions.Logging;

namespace API.Services.Plus;

public class PlusSeriesDto
{
    public int? AniListId { get; set; }
    public int? MalId { get; set; }
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
}

public interface IRecommendationService
{
    Task<IList<SeriesDto>> GetRecommendationsForSeries(int userId, int seriesId);
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

    public async Task<IList<SeriesDto>> GetRecommendationsForSeries(int userId, int seriesId)
    {
        var series =
            await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId,
                SeriesIncludes.Metadata | SeriesIncludes.Library | SeriesIncludes.Volumes | SeriesIncludes.Chapters);
        var seriesRecs = new List<SeriesDto>();
        if (series == null) return seriesRecs;
        var license = await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey);

        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);
        var canSeeExternalSeries = user != null && user.AgeRestriction == AgeRating.NotApplicable &&
                                   await _unitOfWork.UserRepository.IsUserAdminAsync(user);



        var recs = await GetRecommendations(license.Value, series);
        foreach (var rec in recs)
        {
            // Find the series based on name and type and that the user has access too
            var seriesForRec = await _unitOfWork.SeriesRepository.GetSeriesDtoByNamesForUser(userId, rec.RecommendationNames,
                series.Library.Type);
            if (seriesForRec == null)
            {
                if (!canSeeExternalSeries) continue;
                // We can show this based on user permissions

                continue;
            }
            seriesRecs.Add(seriesForRec);
        }

        await _unitOfWork.SeriesRepository.AddSeriesModifiers(userId, seriesRecs);
        return seriesRecs.DistinctBy(s => s.Id).ToList();
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
                .PostJsonAsync(new PlusSeriesDto()
                {
                    MediaFormat = LibraryTypeHelper.GetFormat(series.Library.Type),
                    SeriesName = series.Name,
                    AltSeriesName = series.LocalizedName,
                    AniListId = ScrobblingService.ExtractId(series.Metadata.WebLinks,
                        ScrobblingService.AniListWeblinkWebsite),
                    MalId = ScrobblingService.ExtractId(series.Metadata.WebLinks,
                        ScrobblingService.MalWeblinkWebsite),
                    VolumeCount = series.Volumes.Count,
                    ChapterCount = series.Volumes.SelectMany(v => v.Chapters).Count(c => !c.IsSpecial),
                    Year = series.Metadata.ReleaseYear
                })
                .ReceiveJson<IEnumerable<MediaRecommendationDto>>();

        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error happened during the request to KavitaPlus API");
        }

        return new List<MediaRecommendationDto>();
    }
}
