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
            FilterField.ReleaseYear => string.IsNullOrEmpty(value) ? 0 : int.Parse(value),
            FilterField.Languages => value.Split(',').ToList(),
            FilterField.PublicationStatus => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(x => (PublicationStatus) Enum.Parse(typeof(PublicationStatus), x))
                .ToList(),
            FilterField.Summary => value,
            FilterField.AgeRating => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(x => (AgeRating) Enum.Parse(typeof(AgeRating), x))
                .ToList(),
            FilterField.UserRating => string.IsNullOrEmpty(value) ? 0 : float.Parse(value),
            FilterField.Tags => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            FilterField.CollectionTags => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            FilterField.Translators => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            FilterField.Characters => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            FilterField.Publisher => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            FilterField.Editor => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            FilterField.CoverArtist => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            FilterField.Letterer => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            FilterField.Colorist => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            FilterField.Inker => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            FilterField.Imprint => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            FilterField.Team => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            FilterField.Location => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            FilterField.Penciller => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            FilterField.Writers => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            FilterField.Genres => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            FilterField.Libraries => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            FilterField.WantToRead => bool.Parse(value),
            FilterField.ReadProgress => string.IsNullOrEmpty(value) ? 0f : value.AsFloat(),
            FilterField.ReadingDate => DateTime.Parse(value),
            FilterField.ReadLast => int.Parse(value),
            FilterField.Formats => value.Split(',')
                .Select(x => (MangaFormat) Enum.Parse(typeof(MangaFormat), x))
                .ToList(),
            FilterField.ReadTime => string.IsNullOrEmpty(value) ? 0 : int.Parse(value),
            FilterField.AverageRating => string.IsNullOrEmpty(value) ? 0f : value.AsFloat(),
            _ => throw new ArgumentException("Invalid field type")
        };
    }
}
