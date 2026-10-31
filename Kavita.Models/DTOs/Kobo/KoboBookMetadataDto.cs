#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DTOs.Kobo;

public sealed record KoboBookMetadataDto
{

    private const string DummyId = "00000000-0000-0000-0000-000000000001";

    [JsonIgnore]
    public int Id { get; set; }
    [JsonIgnore]
    public LibraryType LibraryType { get; set; }
    [JsonIgnore]
    public long FileSize { get; set; }
    [JsonIgnore]
    public int FileCount { get; set; }
    [JsonIgnore]
    public MangaFormat Format { get; set; }

    public string WorkId { get; set; }
    public string CoverImageId { get; set; }
    public string RevisionId { get; set; }
    public string CrossRevisionId { get; set; }
    public string EntitlementId { get; set; }
    public List<string> ExternalIds { get; set; } = [];

    public KoboAmountDto CurrentDisplayPrice  { get; set; } = new() { CurrencyCode = "USD", TotalAmount = 0};
    public KoboAmountDto CurrentLoveDisplayPrice  { get; set; } = new();

    public string Title { get; set; }
    public List<string> Categories { get; set; } = [DummyId];
    public List<string> Contributors { get; set; }
    public string Description { get; set; }
    public List<KoboDownloadUrlDto> DownloadUrls { get; set; }
    public string Genre { get; set; } = DummyId;
    /// <summary>
    /// Should be the two-letter code
    /// </summary>
    public string Language { get; set; }
    public Dictionary<string, string> PhoneticPronunciations { get; set; } = new();
    public DateTime PublicationDate { get; set; }
    public KoboPublisherDto Publisher { get; set; }
    public KoboSeriesDto? Series { get; set; }

    public bool IsEligibleForKoboLove { get; set; } = false;
    public bool IsInternetArchive { get; set; } = false;
    public bool IsPreOrder { get; set; } = false;
    public bool IsSocialEnabled { get; set; } = true;

    public static KoboBookMetadataDto DeletedBook(string id)
    {
        return new KoboBookMetadataDto
        {
            WorkId = id,
            CoverImageId = id,
            RevisionId = id,
            CrossRevisionId = id,
            EntitlementId = id,
            Title = id,
        };
    }
}

public sealed record KoboSeriesDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Number { get; set; }
    public float NumberFloat { get; set; }
}
