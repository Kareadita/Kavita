using System;
using System.Collections.Generic;
using API.DTOs.Filtering;
using API.Entities.Enums;

namespace API.Extensions;
#nullable enable

public static class FilterDtoExtensions
{
    private static readonly IList<MangaFormat> AllFormats = Enum.GetValues<MangaFormat>();

    public static IList<MangaFormat> GetSqlFilter(this FilterDto filter)
    {
        if (filter.Formats == null || filter.Formats.Count == 0)
        {
            return AllFormats;
        }

        return filter.Formats;
    }
}
