#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Kavita.Models.DTOs.Kobo;

public sealed record KoboBookRecommendationDto
{
    public string Id { get; set; }
    public string WorkId { get; set; }
    public string CrossRevisionId { get; set; }
    public string KoboId { get; set; }

    public string Title { get; set; }
    public string Subtitle { get; set; } = string.Empty;
    public string Description { get; set; }
    /// <summary>
    /// Should be the two-letter code
    /// </summary>
    public string Language { get; set; }
    public KoboLocaleDto Locale { get; set; }

    [JsonPropertyName("ISBN")]
    public string ISBN { get; set; }
    public string ImageId { get; set; }
    public string PublisherName { get; set; }
    public string Slug { get; set; } = string.Empty;

    public string Contributors { get; set; }
    public List<KoboContributorRoleDto> ContributorRoles { get; set; } = [];

    public KoboPriceDto Price { get; set; } = new();
    public decimal? WasPrice { get; set; }

    public float Rating { get; set; }
    public int TotalRating { get; set; }

    public DateTime PublicationDate { get; set; }

    public KoboSeriesDto? Series { get; set; }

    public List<KoboRedirectPreviewUrlDto> RedirectPreviewUrls { get; set; } = [];
    public List<string> ApplicableSubscriptions { get; set; } = [];

    public bool IsFree { get; set; } = false;
    public bool IsRecommendation { get; set; } = true;
    public bool IsPreOrder { get; set; } = false;
    public bool HasPreview { get; set; } = false;
    public bool EligibleForKoboLoveDiscount { get; set; } = false;
    public bool IsContentSharingEnabled { get; set; } = true;
    public bool AgeVerificationRequired { get; set; } = false;
}

public sealed record KoboContributorRoleDto
{
    public string Name { get; set; }
    public string? Role { get; set; }
}

public sealed record KoboPriceDto
{
    public string Currency { get; set; } = "EUR";
    public decimal Price { get; set; }
}

public sealed record KoboRedirectPreviewUrlDto
{
    public string DrmType { get; set; }
    public string Format { get; set; }
    public string Url { get; set; }
    public string Platform { get; set; }
    public long Size { get; set; }
}
