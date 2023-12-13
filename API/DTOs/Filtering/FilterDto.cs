using System.Collections.Generic;
using API.Entities;
using API.Entities.Enums;

namespace API.DTOs.Filtering;
#nullable enable

public class FilterDto
{
    /// <summary>
    /// The type of Formats you want to be returned. An empty list will return all formats back
    /// </summary>
    public IList<MangaFormat> Formats { get; init; } = new List<MangaFormat>();

    /// <summary>
    /// The progress you want to be returned. This can be bitwise manipulated. Defaults to all applicable states.
    /// </summary>
    public ReadStatus ReadStatus { get; init; } = new ReadStatus();

    /// <summary>
    /// A list of library ids to restrict search to. Defaults to all libraries by passing empty list
    /// </summary>
    public IList<int> Libraries { get; init; } = new List<int>();
    /// <summary>
    /// A list of Genre ids to restrict search to. Defaults to all genres by passing an empty list
    /// </summary>
    public IList<int> Genres { get; init; } = new List<int>();
    /// <summary>
    /// A list of Writers to restrict search to. Defaults to all Writers by passing an empty list
    /// </summary>
    public IList<int> Writers { get; init; } = new List<int>();
    /// <summary>
    /// A list of Penciller ids to restrict search to. Defaults to all Pencillers by passing an empty list
    /// </summary>
    public IList<int> Penciller { get; init; } = new List<int>();
    /// <summary>
    /// A list of Inker ids to restrict search to. Defaults to all Inkers by passing an empty list
    /// </summary>
    public IList<int> Inker { get; init; } = new List<int>();
    /// <summary>
    /// A list of Colorist ids to restrict search to. Defaults to all Colorists by passing an empty list
    /// </summary>
    public IList<int> Colorist { get; init; } = new List<int>();
    /// <summary>
    /// A list of Letterer ids to restrict search to. Defaults to all Letterers by passing an empty list
    /// </summary>
    public IList<int> Letterer { get; init; } = new List<int>();
    /// <summary>
    /// A list of CoverArtist ids to restrict search to. Defaults to all CoverArtists by passing an empty list
    /// </summary>
    public IList<int> CoverArtist { get; init; } = new List<int>();
    /// <summary>
    /// A list of Editor ids to restrict search to. Defaults to all Editors by passing an empty list
    /// </summary>
    public IList<int> Editor { get; init; } = new List<int>();
    /// <summary>
    /// A list of Publisher ids to restrict search to. Defaults to all Publishers by passing an empty list
    /// </summary>
    public IList<int> Publisher { get; init; } = new List<int>();
    /// <summary>
    /// A list of Character ids to restrict search to. Defaults to all Characters by passing an empty list
    /// </summary>
    public IList<int> Character { get; init; } = new List<int>();
    /// <summary>
    /// A list of Translator ids to restrict search to. Defaults to all Translatorss by passing an empty list
    /// </summary>
    public IList<int> Translators { get; init; } = new List<int>();
    /// <summary>
    /// A list of Collection Tag ids to restrict search to. Defaults to all Collection Tags by passing an empty list
    /// </summary>
    public IList<int> CollectionTags { get; init; } = new List<int>();
    /// <summary>
    /// A list of Tag ids to restrict search to. Defaults to all Tags by passing an empty list
    /// </summary>
    public IList<int> Tags { get; init; } = new List<int>();
    /// <summary>
    /// Will return back everything with the rating and above
    /// <see cref="AppUserRating.Rating"/>
    /// </summary>
    public int Rating { get; init; }
    /// <summary>
    /// Sorting Options for a query. Defaults to null, which uses the queries natural sorting order
    /// </summary>
    public SortOptions? SortOptions { get; set; } = null;
    /// <summary>
    /// Age Ratings. Empty list will return everything back
    /// </summary>
    public IList<AgeRating> AgeRating { get; init; } = new List<AgeRating>();
    /// <summary>
    /// Languages (ISO 639-1 code) to filter by. Empty list will return everything back
    /// </summary>
    public IList<string> Languages { get; init; } = new List<string>();
    /// <summary>
    /// Publication statuses to filter by. Empty list will return everything back
    /// </summary>
    public IList<PublicationStatus> PublicationStatus { get; init; } = new List<PublicationStatus>();

    /// <summary>
    /// An optional name string to filter by. Empty string will ignore.
    /// </summary>
    public string SeriesNameQuery { get; init; } = string.Empty;
    /// <summary>
    /// An optional release year to filter by. Null will ignore. You can pass 0 for an individual field to ignore it.
    /// </summary>
    public Range<int>? ReleaseYearRange { get; init; } = null;
}
