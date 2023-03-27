using System;
using System.Collections.Generic;
using System.Linq;
using API.DTOs.Metadata;
using API.Extensions.QueryExtensions.Filtering;

namespace API.DTOs.Filtering.v2;

/// <summary>
/// Represents the field which will dictate the value type and the Extension used for filtering
/// </summary>
public enum FilterField
{
    Summary = 0,
    SeriesName = 1,
    PublicationStatus = 2,
    Languages = 3,
    AgeRating = 4,
    UserRating = 5,
    Tags = 6,
    CollectionTags = 7,
    Translators = 8,
    Characters = 9,
    Publisher = 10,
    Editor = 11,
    CoverArtist = 12,
    Letterer = 13,
    Colorist = 14,
    Inker = 15,
    Penciller = 16,
    Writers = 17,
    Genres = 18,
    Libraries = 19,
    ReadProgress = 20,
    Formats = 21,
    ReleaseYear = 22,
    ReadTime = 23
}

/// <summary>
/// Metadata filtering for v2 API only
/// </summary>
public class FilterV2Dto
{
    public string? Name { get; set; }
    public ICollection<FilterGroupDto> Groups { get; set; }
}

public class FilterGroupDto
{
    public FilterComparison Comparison { get; set; }
    public FilterField Field { get; set; }
    public string Value { get; set; }
}


