using System;
using System.Globalization;
using System.Linq;
using Kavita.Common.Extensions;
using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.Entities.Enums;

namespace Kavita.Database.Converters;

public static class SeriesFilterFieldValueConverter
{
    public static object ConvertValue(SeriesFilterField field, string value)
    {
        return field switch
        {
            SeriesFilterField.SeriesName => value,
            SeriesFilterField.Path => value,
            SeriesFilterField.FilePath => value,
            SeriesFilterField.ReleaseYear => string.IsNullOrEmpty(value) ? 0 : int.Parse(value),
            SeriesFilterField.Languages => value.Split(',').ToList(),
            SeriesFilterField.PublicationStatus => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(x => (PublicationStatus) Enum.Parse(typeof(PublicationStatus), x))
                .ToList(),
            SeriesFilterField.Summary => value,
            SeriesFilterField.AgeRating => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(x => (AgeRating) Enum.Parse(typeof(AgeRating), x))
                .ToList(),
            SeriesFilterField.UserRating => string.IsNullOrEmpty(value) ? 0 : float.Parse(value),
            SeriesFilterField.Tags => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            SeriesFilterField.CollectionTags => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            SeriesFilterField.Translators => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            SeriesFilterField.Characters => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            SeriesFilterField.Publisher => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            SeriesFilterField.Editor => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            SeriesFilterField.CoverArtist => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            SeriesFilterField.Letterer => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            SeriesFilterField.Colorist => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            SeriesFilterField.Inker => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            SeriesFilterField.Imprint => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            SeriesFilterField.Team => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            SeriesFilterField.Location => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            SeriesFilterField.Penciller => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            SeriesFilterField.Writers => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            SeriesFilterField.Genres => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            SeriesFilterField.Libraries => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            SeriesFilterField.WantToRead => bool.Parse(value),
            SeriesFilterField.ReadProgress => string.IsNullOrEmpty(value) ? 0f : value.AsFloat(),
            SeriesFilterField.ReadingDate => DateTime.Parse(value, CultureInfo.InvariantCulture),
            SeriesFilterField.ReadLast => int.Parse(value),
            SeriesFilterField.Formats => value.Split(',')
                .Select(x => (MangaFormat) Enum.Parse(typeof(MangaFormat), x))
                .ToList(),
            SeriesFilterField.ReadTime => string.IsNullOrEmpty(value) ? 0 : int.Parse(value),
            SeriesFilterField.AverageRating => string.IsNullOrEmpty(value) ? 0f : value.AsFloat(),
            SeriesFilterField.FileSize => value.ParseHumanReadableBytes(),
            _ => throw new ArgumentException("Invalid field type")
        };
    }
}
