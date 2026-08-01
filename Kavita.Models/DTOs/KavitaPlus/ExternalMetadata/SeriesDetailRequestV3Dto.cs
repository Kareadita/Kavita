using System.Collections.Generic;
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.KavitaPlus;
using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.DTOs.KavitaPlus.ExternalMetadata;
#nullable enable

public sealed record SeriesDetailRequestV3Dto: MetadataRequest
{
    [EnumDataType(typeof(MetadataProvider))]
    public required MetadataProvider Provider { get; set; }
    public required string SeriesName { get; set; }
    public List<string> AlternativeNames { get; set; } = [];
    [EnumDataType(typeof(PlusMediaFormat))]
    public PlusMediaFormat Format { get; set; }
    public int? ChapterCount { get; set; }
    public int? VolumeCount { get; set; }
    public int? Year { get; set; }

    /// <summary>
    /// Include Reviews
    /// </summary>
    /// <remarks>Make false for Recommendation data retrieval</remarks>
    public bool IncludeReviews { get; set; } = true;
    /// <summary>
    /// Include Recommendations
    /// </summary>
    /// <remarks>Make false for Recommendation data retrieval</remarks>
    public bool IncludeRecommendations { get; set; } = true;
    /// <summary>
    /// Include Relationships
    /// </summary>
    /// <remarks>Make false for Recommendation data retrieval</remarks>
    public bool IncludeRelationships { get; set; } = true;

    /// <summary>
    /// Projects a v2 <see cref="PlusSeriesRequestDto"/> into the v3 series-detail request contract.
    /// </summary>
    /// <param name="data">The v2 request data</param>
    /// <param name="provider">The provider to route the request to (derived from the primary id)</param>
    public static SeriesDetailRequestV3Dto From(PlusSeriesRequestDto data, MetadataProvider provider)
    {
        return new SeriesDetailRequestV3Dto
        {
            Provider = provider,
            SeriesName = data.SeriesName,
            AlternativeNames = string.IsNullOrWhiteSpace(data.AltSeriesName) ? [] : [data.AltSeriesName],
            Format = data.MediaFormat,
            ChapterCount = data.ChapterCount,
            VolumeCount = data.VolumeCount,
            Year = data.Year,
            // Ids carried on the MetadataRequest base
            AniListId = data.AniListId,
            MalId = data.MalId,
            HardcoverId = data.HardcoverId,
            CbrId = data.CbrId,
            MangabakaId = data.MangabakaId,
            GoogleBooksId = data.GoogleBooksId,
            MangaDexId = data.MangaDexId,
            // No v2 source for MetronId / ComicVineId / IsStandAlone; leave defaults
        };
    }
}