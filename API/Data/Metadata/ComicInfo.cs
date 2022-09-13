using System;
using System.Linq;
using API.Entities.Enums;
using Kavita.Common.Extensions;

namespace API.Data.Metadata;

/// <summary>
/// A representation of a ComicInfo.xml file
/// </summary>
/// <remarks>See reference of the loose spec here: https://anansi-project.github.io/docs/comicinfo/documentation</remarks>
public class ComicInfo
{
    public string Summary { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Series { get; set; } = string.Empty;
    /// <summary>
    /// Localized Series name. Not standard.
    /// </summary>
    public string LocalizedSeries { get; set; } = string.Empty;
    public string SeriesSort { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    /// <summary>
    /// The total number of items in the series.
    /// </summary>
    public int Count { get; set; } = 0;
    public string Volume { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public int PageCount { get; set; }
    // ReSharper disable once InconsistentNaming
    /// <summary>
    /// IETF BCP 47 Code to represent the language of the content
    /// </summary>
    public string LanguageISO { get; set; } = string.Empty;
    /// <summary>
    /// This is the link to where the data was scraped from
    /// </summary>
    public string Web { get; set; } = string.Empty;
    public int Day { get; set; } = 0;
    public int Month { get; set; } = 0;
    public int Year { get; set; } = 0;


    /// <summary>
    /// Rating based on the content. Think PG-13, R for movies. See <see cref="AgeRating"/> for valid types
    /// </summary>
    public string AgeRating { get; set; } = string.Empty;
    /// <summary>
    /// User's rating of the content
    /// </summary>
    public float UserRating { get; set; }

    public string StoryArc { get; set; } = string.Empty;
    public string SeriesGroup { get; set; } = string.Empty;
    public string AlternateNumber { get; set; } = string.Empty;
    public int AlternateCount { get; set; } = 0;
    public string AlternateSeries { get; set; } = string.Empty;

    /// <summary>
    /// This is Epub only: calibre:title_sort
    /// Represents the sort order for the title
    /// </summary>
    public string TitleSort { get; set; } = string.Empty;
    /// <summary>
    /// This comes from ComicInfo and is free form text. We use this to validate against a set of tags and mark a file as
    /// special.
    /// </summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>
    /// The translator, can be comma separated. This is part of ComicInfo.xml draft v2.1
    /// </summary>
    /// See https://github.com/anansi-project/comicinfo/issues/2 for information about this tag
    public string Translator { get; set; } = string.Empty;
    /// <summary>
    /// Misc tags. This is part of ComicInfo.xml draft v2.1
    /// </summary>
    /// See https://github.com/anansi-project/comicinfo/issues/1 for information about this tag
    public string Tags { get; set; } = string.Empty;

    /// <summary>
    /// This is the Author. For Books, we map creator tag in OPF to this field. Comma separated if multiple.
    /// </summary>
    public string Writer { get; set; } = string.Empty;
    public string Penciller { get; set; } = string.Empty;
    public string Inker { get; set; } = string.Empty;
    public string Colorist { get; set; } = string.Empty;
    public string Letterer { get; set; } = string.Empty;
    public string CoverArtist { get; set; } = string.Empty;
    public string Editor { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string Characters { get; set; } = string.Empty;

    public static AgeRating ConvertAgeRatingToEnum(string value)
    {
        if (string.IsNullOrEmpty(value)) return Entities.Enums.AgeRating.Unknown;
        return Enum.GetValues<AgeRating>()
            .SingleOrDefault(t => t.ToDescription().ToUpperInvariant().Equals(value.ToUpperInvariant()), Entities.Enums.AgeRating.Unknown);
    }

    public static void CleanComicInfo(ComicInfo info)
    {
        if (info == null) return;

        info.Series = info.Series.Trim();
        info.SeriesSort = info.SeriesSort.Trim();
        info.LocalizedSeries = info.LocalizedSeries.Trim();

        info.Writer = Services.Tasks.Scanner.Parser.Parser.CleanAuthor(info.Writer);
        info.Colorist = Services.Tasks.Scanner.Parser.Parser.CleanAuthor(info.Colorist);
        info.Editor = Services.Tasks.Scanner.Parser.Parser.CleanAuthor(info.Editor);
        info.Inker = Services.Tasks.Scanner.Parser.Parser.CleanAuthor(info.Inker);
        info.Letterer = Services.Tasks.Scanner.Parser.Parser.CleanAuthor(info.Letterer);
        info.Penciller = Services.Tasks.Scanner.Parser.Parser.CleanAuthor(info.Penciller);
        info.Publisher = Services.Tasks.Scanner.Parser.Parser.CleanAuthor(info.Publisher);
        info.Characters = Services.Tasks.Scanner.Parser.Parser.CleanAuthor(info.Characters);
        info.Translator = Services.Tasks.Scanner.Parser.Parser.CleanAuthor(info.Translator);
        info.CoverArtist = Services.Tasks.Scanner.Parser.Parser.CleanAuthor(info.CoverArtist);
    }


}
