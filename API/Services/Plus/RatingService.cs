using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using Flurl.Http;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Kavita.Common.Helpers;
using Microsoft.Extensions.Logging;

namespace API.Services.Plus;

public interface IRatingService
{
    Task<IEnumerable<RatingDto>> GetRatings(int seriesId);
}

public class RatingService : IRatingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RatingService> _logger;

    public RatingService(IUnitOfWork unitOfWork, ILogger<RatingService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;

        FlurlHttp.ConfigureClient(Configuration.KavitaPlusApiUrl, cli =>
            cli.Settings.HttpClientFactory = new UntrustedCertClientFactory());
    }

    public async Task<IEnumerable<RatingDto>> GetRatings(int seriesId)
    {
        var license = await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey);
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId,
            SeriesIncludes.Metadata | SeriesIncludes.Library | SeriesIncludes.Chapters | SeriesIncludes.Volumes);
        return await GetRatings(license.Value, series);
    }

    private async Task<IEnumerable<RatingDto>> GetRatings(string license, Series series)
    {
        var serverSetting = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        try
        {
            return await (Configuration.KavitaPlusApiUrl + "/api/rating")
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .WithHeader("x-license-key", license)
                .WithHeader("x-installId", serverSetting.InstallId)
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
                .ReceiveJson<IEnumerable<RatingDto>>();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error happened during the request to KavitaPlus API");
        }

        return new List<RatingDto>();
    }
}
