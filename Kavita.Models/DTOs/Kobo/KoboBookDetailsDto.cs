#nullable enable
using System;
using System.Collections.Generic;

namespace Kavita.Models.DTOs.Kobo;

public sealed record KoboBookDetailsDto
{
    public string WorkId { get; set; }
    public string CrossRevisionId { get; set; }
    public string KoboId { get; set; }

    public string Title { get; set; }
    public string Subtitle { get; set; }
    public string Description { get; set; }
    /// <summary>
    /// Should be the two-letter code
    /// </summary>
    public string Language { get; set; }
    public KoboLocaleDto Locale { get; set; }

    public string ISBN { get; set; }
    public string ImageId { get; set; }
    public string PublisherName { get; set; }
    public string Slug { get; set; }

    public string Contributors { get; set; }
    public List<KoboContributorRoleDto> ContributorRoles { get; set; } = [];

    public KoboPriceDto Price { get; set; } = new();
    public decimal? WasPrice { get; set; }

    public float Rating { get; set; }
    public int TotalRating { get; set; }
    public Dictionary<string, int> RatingHistogram { get; set; } = new();

    public DateTime PublicationDate { get; set; }

    public string SeriesName { get; set; }
    public string SeriesNumber { get; set; }
    public string SeriesSlug { get; set; }

    public KoboRelatedGroupDto? RelatedGroup { get; set; }

    public List<KoboRedirectPreviewUrlDto> RedirectPreviewUrls { get; set; } = [];
    public List<string> ApplicableSubscriptions { get; set; } = [];

    public KoboStatsDto Stats { get; set; }
    public KoboAccessibilityDetailsDto? AccessibilityDetails { get; set; }

    public bool IsFree { get; set; } = false;
    public bool IsRecommendation { get; set; } = false;
    public bool IsPreOrder { get; set; } = false;
    public bool HasPreview { get; set; } = false;
    public bool EligibleForKoboLoveDiscount { get; set; } = false;
    public bool IsContentSharingEnabled { get; set; } = true;
    public bool IsInternetArchive { get; set; } = false;
    public bool InWishlist { get; set; } = false;
    public bool PromoCodeAllowed { get; set; } = false;
    public bool AgeVerificationRequired { get; set; } = false;
}

public sealed record KoboLocaleDto
{
    /// <summary>
    /// Three-letter ISO 639-2 code, e.g. "fre"
    /// </summary>
    public string LanguageCode { get; set; }
}

public sealed record KoboRelatedGroupDto
{
    public string Id { get; set; }
}

public sealed record KoboStatsDto
{
    public int Pages { get; set; }
    public List<string> PagesInThousandGroups { get; set; } = [];
    public KoboWordCountDto SimplifiedWordCount { get; set; }
    public KoboTimeToReadDto TimeToRead { get; set; }
    public int WordCount { get; set; }
}

/// <summary>
/// The wire shape is a 2-tuple serialized as { "Item1": "...", "Item2": ... }
/// </summary>
public sealed record KoboWordCountDto
{
    public string Item1 { get; set; }
    public int Item2 { get; set; }
}

public sealed record KoboTimeToReadDto
{
    public int Item1 { get; set; }
    public int Item2 { get; set; }
}

public sealed record KoboAccessibilityDetailsDto
{
    public List<KoboEpubAccessibilityDto> EPubAccessibilities { get; set; } = [];
    public List<KoboHazardWarningTypeDto> HazardWarningTypes { get; set; } = [];
    public bool IsAccessible { get; set; }
    public bool IsFixedLayout { get; set; }
    public bool IsTextToSpeechAllowed { get; set; }
    public string PrimaryContentType { get; set; }
}

public sealed record KoboEpubAccessibilityDto
{
    public string Type { get; set; }
    public string? Value { get; set; }
}

public sealed record KoboHazardWarningTypeDto
{
    public string Type { get; set; }
}
