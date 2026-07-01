using System.Globalization;
using Kavita.Common.Helpers;

namespace Kavita.Models.DTOs.Search;
#nullable enable

/// <summary>
/// Represents a parsed search request. <see cref="Query"/> drives the fuzzy name search, while the provider
/// shortcodes (anilist:/al:, mangabaka:/mb:, hardcover:) trigger a direct external-id lookup instead.
/// </summary>
public sealed class SearchDto
{
    /// <summary>
    /// Free-text query for the fuzzy name search. Empty when a shortcode was supplied.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Include Chapters and Files in the results. This can slow down the search on larger systems.
    /// </summary>
    public bool IncludeChapterAndFiles { get; set; } = true;

    public int? AniListId { get; set; }
    public long? MangaBakaId { get; set; }
    public int? HardcoverId { get; set; }

    /// <summary>
    /// True when a provider shortcode was parsed out of the query and a direct external-id lookup should run.
    /// </summary>
    public bool HasShortcode => AniListId.HasValue || MangaBakaId.HasValue || HardcoverId.HasValue;

    /// <summary>
    /// Parses provider shortcodes (anilist:/al:, mangabaka:/mb:, hardcover:) out of the raw query.
    /// Must be called on the raw query <b>before</b> any cleaning that strips ':'. When a shortcode matches,
    /// <see cref="Query"/> is left empty and the caller should not overwrite it.
    /// </summary>
    public static SearchDto FromQuery(string? rawQuery, bool includeChapterAndFiles = true)
    {
        var dto = new SearchDto { IncludeChapterAndFiles = includeChapterAndFiles };

        if (ExternalIdParser.TryParseAniListHeader(rawQuery, out var aniListId))
        {
            dto.AniListId = aniListId;
        }
        else if (ExternalIdParser.TryParseMangaBakaHeader(rawQuery, out var mangaBakaId))
        {
            dto.MangaBakaId = mangaBakaId;
        }
        else if (ExternalIdParser.TryParseHardcoverHeader(rawQuery, out var hardcoverId)
                 && int.TryParse(hardcoverId, CultureInfo.InvariantCulture, out var hardcoverBookId))
        {
            dto.HardcoverId = hardcoverBookId;
        }

        return dto;
    }
}
