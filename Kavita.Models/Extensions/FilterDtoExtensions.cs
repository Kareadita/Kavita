using System;
using System.Collections.Generic;
using Kavita.Models.DTOs.Filtering;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.Extensions;

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
