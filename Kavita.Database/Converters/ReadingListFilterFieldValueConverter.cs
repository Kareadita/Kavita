using System;
using System.Linq;
using Kavita.Models.DTOs.Filtering.v2.FilterFields;
using Kavita.Models.Entities.Enums.ReadingList;

namespace Kavita.Database.Converters;

public static class ReadingListFilterFieldValueConverter
{
    public static object ConvertValue(ReadingListFilterField field, string value)
    {
        return field switch
        {
            ReadingListFilterField.Title => value,
            ReadingListFilterField.ReleaseYear => string.IsNullOrEmpty(value) ? 0 : int.Parse(value),
            ReadingListFilterField.ItemCount => string.IsNullOrEmpty(value) ? 0 : int.Parse(value),
            ReadingListFilterField.MissingItemCount => string.IsNullOrEmpty(value) ? 0 : int.Parse(value),
            ReadingListFilterField.Tags => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            ReadingListFilterField.Writer => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            ReadingListFilterField.Artist => value.Split(',')
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .ToList(),
            ReadingListFilterField.Provider => Enum.Parse<ReadingListProvider>(value),
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Field is not supported")
        };
    }
}
