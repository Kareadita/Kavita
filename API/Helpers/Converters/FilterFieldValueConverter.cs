using System;
using System.Collections.Generic;
using System.Linq;
using API.DTOs.Filtering.v2;
using API.Entities.Enums;

namespace API.Helpers.Converters;

public static class FilterFieldValueConverter
{
    public static (object Value, Type Type) ConvertValue(FilterField field, string value)
    {
        return field switch
        {
            FilterField.SeriesName => (value, typeof(string)),
            FilterField.Path => (value, typeof(string)),
            FilterField.FilePath => (value, typeof(string)),
            FilterField.ReleaseYear => (int.Parse(value), typeof(int)),
            FilterField.Languages => (value.Split(',').ToList(), typeof(IList<string>)),
            FilterField.PublicationStatus => (value.Split(',')
                .Select(x => (PublicationStatus) Enum.Parse(typeof(PublicationStatus), x))
                .ToList(), typeof(IList<PublicationStatus>)),
            FilterField.Summary => (value, typeof(string)),
            FilterField.AgeRating => (value.Split(',')
                .Select(x => (AgeRating) Enum.Parse(typeof(AgeRating), x))
                .ToList(), typeof(IList<AgeRating>)),
            FilterField.UserRating => (int.Parse(value), typeof(int)),
            FilterField.Tags => (value.Split(',')
                .Select(int.Parse)
                .ToList(), typeof(IList<int>)),
            FilterField.CollectionTags => (value.Split(',')
                .Select(int.Parse)
                .ToList(), typeof(IList<int>)),
            FilterField.Translators => (value.Split(',')
                .Select(int.Parse)
                .ToList(), typeof(IList<int>)),
            FilterField.Characters => (value.Split(',')
                .Select(int.Parse)
                .ToList(), typeof(IList<int>)),
            FilterField.Publisher => (value.Split(',')
                .Select(int.Parse)
                .ToList(), typeof(IList<int>)),
            FilterField.Editor => (value.Split(',')
                .Select(int.Parse)
                .ToList(), typeof(IList<int>)),
            FilterField.CoverArtist => (value.Split(',')
                .Select(int.Parse)
                .ToList(), typeof(IList<int>)),
            FilterField.Letterer => (value.Split(',')
                .Select(int.Parse)
                .ToList(), typeof(IList<int>)),
            FilterField.Colorist => (value.Split(',')
                .Select(int.Parse)
                .ToList(), typeof(IList<int>)),
            FilterField.Inker => (value.Split(',')
                .Select(int.Parse)
                .ToList(), typeof(IList<int>)),
            FilterField.Penciller => (value.Split(',')
                .Select(int.Parse)
                .ToList(), typeof(IList<int>)),
            FilterField.Writers => (value.Split(',')
                .Select(int.Parse)
                .ToList(), typeof(IList<int>)),
            FilterField.Genres => (value.Split(',')
                .Select(int.Parse)
                .ToList(), typeof(IList<int>)),
            FilterField.Libraries => (value.Split(',')
                .Select(int.Parse)
                .ToList(), typeof(IList<int>)),
            FilterField.WantToRead => (bool.Parse(value), typeof(bool)),
            FilterField.ReadProgress => (int.Parse(value), typeof(int)),
            FilterField.ReadingDate => (DateTime.Parse(value), typeof(DateTime?)),
            FilterField.Formats => (value.Split(',')
                .Select(x => (MangaFormat) Enum.Parse(typeof(MangaFormat), x))
                .ToList(), typeof(IList<MangaFormat>)),
            FilterField.ReadTime => (int.Parse(value), typeof(int)),
            _ => throw new ArgumentException("Invalid field type")
        };
    }
}
