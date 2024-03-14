using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using API.DTOs.Filtering.v2;
using API.Entities.Enums;
using API.Extensions;

namespace API.Helpers.Converters;
#nullable enable

public static class FilterFieldValueConverter
{
    public static object ConvertValue(FilterField field, string value)
    {
        return field switch
        {
            FilterField.SeriesName => value,
            FilterField.Path => value,
            FilterField.FilePath => value,
            FilterField.ReleaseYear => int.Parse(value),
            FilterField.Languages => value.Split(',').ToList(),
            FilterField.PublicationStatus => value.Split(',')
                .Select(x => (PublicationStatus) Enum.Parse(typeof(PublicationStatus), x))
                .ToList(),
            FilterField.Summary => value,
            FilterField.AgeRating => value.Split(',')
                .Select(x => (AgeRating) Enum.Parse(typeof(AgeRating), x))
                .ToList(),
            FilterField.UserRating => int.Parse(value),
            FilterField.Tags => value.Split(',')
                .Select(int.Parse)
                .ToList(),
            FilterField.CollectionTags => value.Split(',')
                .Select(int.Parse)
                .ToList(),
            FilterField.Translators => value.Split(',')
                .Select(int.Parse)
                .ToList(),
            FilterField.Characters => value.Split(',')
                .Select(int.Parse)
                .ToList(),
            FilterField.Publisher => value.Split(',')
                .Select(int.Parse)
                .ToList(),
            FilterField.Editor => value.Split(',')
                .Select(int.Parse)
                .ToList(),
            FilterField.CoverArtist => value.Split(',')
                .Select(int.Parse)
                .ToList(),
            FilterField.Letterer => value.Split(',')
                .Select(int.Parse)
                .ToList(),
            FilterField.Colorist => value.Split(',')
                .Select(int.Parse)
                .ToList(),
            FilterField.Inker => value.Split(',')
                .Select(int.Parse)
                .ToList(),
            FilterField.Imprint => value.Split(',')
                .Select(int.Parse)
                .ToList(),
            FilterField.Team => value.Split(',')
                .Select(int.Parse)
                .ToList(),
            FilterField.Location => value.Split(',')
                .Select(int.Parse)
                .ToList(),
            FilterField.Penciller => value.Split(',')
                .Select(int.Parse)
                .ToList(),
            FilterField.Writers => value.Split(',')
                .Select(int.Parse)
                .ToList(),
            FilterField.Genres => value.Split(',')
                .Select(int.Parse)
                .ToList(),
            FilterField.Libraries => value.Split(',')
                .Select(int.Parse)
                .ToList(),
            FilterField.WantToRead => bool.Parse(value),
            FilterField.ReadProgress => value.AsFloat(),
            FilterField.ReadingDate => DateTime.Parse(value),
            FilterField.Formats => value.Split(',')
                .Select(x => (MangaFormat) Enum.Parse(typeof(MangaFormat), x))
                .ToList(),
            FilterField.ReadTime => int.Parse(value),
            FilterField.AverageRating => float.Parse(value),
            _ => throw new ArgumentException("Invalid field type")
        };
    }
}
