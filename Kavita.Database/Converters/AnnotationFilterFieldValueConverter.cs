using System;
using Kavita.Common.Extensions;
using Kavita.Models.DTOs.Filtering.v2;

namespace Kavita.Database.Converters;

public static class AnnotationFilterFieldValueConverter
{

    public static object ConvertValue(AnnotationFilterField field, string value)
    {
        return field switch
        {
            AnnotationFilterField.Owner or
                AnnotationFilterField.HighlightSlot or
                AnnotationFilterField.Library or
                AnnotationFilterField.Series => value.ParseIntArray(),
            AnnotationFilterField.Spoiler => bool.Parse(value),
            AnnotationFilterField.Selection => value,
            AnnotationFilterField.Comment => value,
            AnnotationFilterField.Likes => int.Parse(value),
            AnnotationFilterField.LikedBy => value.ParseIntArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Field is not supported")
        };
    }

}
