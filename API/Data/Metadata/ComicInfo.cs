using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using API.Entities;
using API.Entities.Enums;
using API.Services;
using Kavita.Common.Extensions;
using Nager.ArticleNumber;

namespace API.Data.Metadata;
#nullable enable

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
    [System.ComponentModel.DefaultValueAttribute(0)]
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

    // ReSharper disable once InconsistentNaming
    /// <summary>
    /// ISBN for the underlying document
    /// </summary>
    /// <remarks>ComicInfo.xml will actually output a GTIN (Global Trade Item Number) and it is the responsibility of the Parser to extract the ISBN. EPub will return ISBN.</remarks>
    public string Isbn { get; set; } = string.Empty;
    /// <summary>
    /// This is only for deserialization and used within <see cref="ArchiveService"/>. Use <see cref="Isbn"/> for the actual value.
    /// </summary>
    public string GTIN { get; set; } = string.Empty;
    /// <summary>
    /// This is the link to where the data was scraped from
    /// </summary>
    /// <remarks>This can be comma-separated</remarks>
    public string Web { get; set; } = string.Empty;
    [System.ComponentModel.DefaultValueAttribute(0)]
    public int Day { get; set; } = 0;
    [System.ComponentModel.DefaultValueAttribute(0)]
    public int Month { get; set; } = 0;
    [System.ComponentModel.DefaultValueAttribute(0)]
    public int Year { get; set; } = 0;


    /// <summary>
    /// Rating based on the content. Think PG-13, R for movies. See <see cref="AgeRating"/> for valid types
    /// </summary>
    public string AgeRating { get; set; } = string.Empty;
    /// <summary>
    /// User's rating of the content
    /// </summary>
    public float UserRating { get; set; }
    /// <summary>
    /// Can contain multiple comma separated strings, each create a <see cref="CollectionTag"/>
    /// </summary>
    public string SeriesGroup { get; set; } = string.Empty;

    /// <summary>
    /// Can contain multiple comma separated numbers that match with StoryArcNumber
    /// </summary>
    public string StoryArc { get; set; } = string.Empty;
    /// <summary>
    /// Can contain multiple comma separated numbers that match with StoryArc
    /// </summary>
    public string StoryArcNumber { get; set; } = string.Empty;
    public string AlternateNumber { get; set; } = string.Empty;
    public string AlternateSeries { get; set; } = string.Empty;

    /// <summary>
    /// Not used
    /// </summary>
    [System.ComponentModel.DefaultValueAttribute(0)]
    public int AlternateCount { get; set; } = 0;

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
    public string Imprint { get; set; } = string.Empty;
    public string Characters { get; set; } = string.Empty;
    public string Teams { get; set; } = string.Empty;
    public string Locations { get; set; } = string.Empty;


    public static AgeRating ConvertAgeRatingToEnum(string value)
    {
        if (string.IsNullOrEmpty(value)) return Entities.Enums.AgeRating.Unknown;
        return Enum.GetValues<AgeRating>()
            .SingleOrDefault(t => t.ToDescription().ToUpperInvariant().Equals(value.ToUpperInvariant()), Entities.Enums.AgeRating.Unknown);
    }

    public static void CleanComicInfo(ComicInfo? info)
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
        info.Imprint = Services.Tasks.Scanner.Parser.Parser.CleanAuthor(info.Imprint);
        info.Characters = Services.Tasks.Scanner.Parser.Parser.CleanAuthor(info.Characters);
        info.Translator = Services.Tasks.Scanner.Parser.Parser.CleanAuthor(info.Translator);
        info.CoverArtist = Services.Tasks.Scanner.Parser.Parser.CleanAuthor(info.CoverArtist);
        info.Teams = Services.Tasks.Scanner.Parser.Parser.CleanAuthor(info.Teams);
        info.Locations = Services.Tasks.Scanner.Parser.Parser.CleanAuthor(info.Locations);

        // We need to convert GTIN to ISBN
        if (!string.IsNullOrEmpty(info.GTIN))
        {
            // This is likely a valid ISBN
            if (info.GTIN[0] == '0')
            {
                var potentialISBN = info.GTIN.Substring(1, info.GTIN.Length - 1);
                if (ArticleNumberHelper.IsValidIsbn13(potentialISBN))
                {
                    info.Isbn = potentialISBN;
                }
            } else if (ArticleNumberHelper.IsValidIsbn10(info.GTIN) || ArticleNumberHelper.IsValidIsbn13(info.GTIN))
            {
                info.Isbn = info.GTIN;
            }
        }

        if (!string.IsNullOrEmpty(info.Number))
        {
            info.Number = info.Number.Trim().Replace(",", "."); // Corrective measure for non English OSes
        }

        if (!string.IsNullOrEmpty(info.Volume))
        {
            info.Volume = info.Volume.Trim();
        }
    }

    /// <summary>
    /// Uses both Volume and Number to make an educated guess as to what count refers to and it's highest number.
    /// </summary>
    /// <returns></returns>
    public int CalculatedCount()
    {
        try
        {
            if (float.TryParse(Number, CultureInfo.InvariantCulture, out var chpCount) && chpCount > 0)
            {
                return (int) Math.Floor(chpCount);
            }

            if (float.TryParse(Volume, CultureInfo.InvariantCulture, out var volCount) && volCount > 0)
            {
                return (int) Math.Floor(volCount);
            }
        }
        catch (Exception)
        {
            return 0;
        }

        return 0;
    }


}
