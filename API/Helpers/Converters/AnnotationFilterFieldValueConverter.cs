using System;
using System.Linq;
using API.DTOs.Filtering.v2;
using API.Extensions;

namespace API.Helpers.Converters;

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
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Field is not supported")
        };
    }

}
