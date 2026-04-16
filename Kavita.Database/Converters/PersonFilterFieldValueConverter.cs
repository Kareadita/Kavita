using System;
using System.Collections.Generic;
using System.Linq;
using Kavita.Common.Extensions;
using Kavita.Models.DTOs.Filtering.v2.FilterFields;
using Kavita.Models.Entities.Enums;

namespace Kavita.Database.Converters;

public static class PersonFilterFieldValueConverter
{
    public static object ConvertValue(PersonFilterField field, string value)
    {
        return field switch
        {
            PersonFilterField.Name => value,
            PersonFilterField.Role => ParsePersonRoles(value),
            PersonFilterField.SeriesCount => int.Parse(value),
            PersonFilterField.ChapterCount => int.Parse(value),
            PersonFilterField.Library => value.ParseIntArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Field is not supported")
        };
    }

    private static List<PersonRole> ParsePersonRoles(string value)
    {
        if (string.IsNullOrEmpty(value)) return [];

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(v => Enum.Parse<PersonRole>(v.Trim()))
            .ToList();
    }
}
