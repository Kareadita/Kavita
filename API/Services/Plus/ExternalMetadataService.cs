using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Recommendation;
using API.DTOs.Scrobbling;
using API.Entities.Enums;
using API.Helpers.Builders;
using Flurl.Http;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Kavita.Common.Helpers;
using Microsoft.Extensions.Logging;

namespace API.Services.Plus;
#nullable enable

/// <summary>
/// Used for matching and fetching metadata on a series
/// </summary>
internal class ExternalMetadataIdsDto
{
    public long? MalId { get; set; }
    public int? AniListId { get; set; }

    public string? SeriesName { get; set; }
    public string? LocalizedSeriesName { get; set; }
    public MediaFormat? PlusMediaFormat { get; set; } = MediaFormat.Unknown;
}

public interface IExternalMetadataService
{
    Task<ExternalSeriesDetailDto> GetExternalSeriesDetail(int? aniListId, long? malId, int? seriesId);
}

public class ExternalMetadataService : IExternalMetadataService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ExternalMetadataService> _logger;

    public ExternalMetadataService(IUnitOfWork unitOfWork, ILogger<ExternalMetadataService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;

        FlurlHttp.ConfigureClient(Configuration.KavitaPlusApiUrl, cli =>
            cli.Settings.HttpClientFactory = new UntrustedCertClientFactory());
    }

    public async Task<ExternalSeriesDetailDto?> GetExternalSeriesDetail(int? aniListId, long? malId, int? seriesId)
    {
        if (!aniListId.HasValue && !malId.HasValue)
        {
            throw new KavitaException("Unable to find valid information from url for External Load");
        }

        var license = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey)).Value;
        return await GetSeriesDetail(license, aniListId, malId, seriesId);

    }

    private async Task<ExternalSeriesDetailDto?> GetSeriesDetail(string license, int? aniListId, long? malId, int? seriesId)
    {
        var payload = new ExternalMetadataIdsDto()
        {
            AniListId = aniListId,
            MalId = malId,
            SeriesName = string.Empty,
            LocalizedSeriesName = string.Empty
        };
        if (seriesId is > 0)
        {
            var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId.Value, SeriesIncludes.Metadata | SeriesIncludes.Library);
            if (series != null)
            {
                if (payload.AniListId <= 0)
                {
                    payload.AniListId = ScrobblingService.ExtractId<int>(series.Metadata.WebLinks, ScrobblingService.AniListWeblinkWebsite);
                }
                if (payload.MalId <= 0)
                {
                    payload.MalId = ScrobblingService.ExtractId<long>(series.Metadata.WebLinks, ScrobblingService.MalWeblinkWebsite);
                }
                payload.SeriesName = series.Name;
                payload.LocalizedSeriesName = series.LocalizedName;
                payload.PlusMediaFormat = ConvertToMediaFormat(series.Library.Type, series.Format);
            }

        }
        try
        {
            return await (Configuration.KavitaPlusApiUrl + "/api/metadata/series/detail")
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .WithHeader("x-license-key", license)
                .WithHeader("x-installId", HashUtil.ServerToken())
                .WithHeader("x-kavita-version", BuildInfo.Version)
                .WithHeader("Content-Type", "application/json")
                .WithTimeout(TimeSpan.FromSeconds(Configuration.DefaultTimeOutSecs))
                .PostJsonAsync(payload)
                .ReceiveJson<ExternalSeriesDetailDto>();

        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error happened during the request to Kavita+ API");
        }

        return null;
    }

    private static MediaFormat ConvertToMediaFormat(LibraryType libraryType, MangaFormat seriesFormat)
    {
        return libraryType switch
        {
            LibraryType.Manga => seriesFormat == MangaFormat.Epub ? MediaFormat.LightNovel : MediaFormat.Manga,
            LibraryType.Comic => MediaFormat.Comic,
            LibraryType.Book => MediaFormat.Book,
            _ => MediaFormat.Unknown
        };
    }
}
