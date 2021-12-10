using System;
using System.Collections.Generic;
using API.DTOs.Filtering;
using API.Entities.Enums;

namespace API.Extensions
{
    public static class FilterDtoExtensions
    {
        private static readonly IList<MangaFormat> AllFormats = Enum.GetValues<MangaFormat>();

        public static IList<MangaFormat> GetSqlFilter(this FilterDto filter)
        {
            var format = filter.MangaFormat;
            if (format != null) /*   || filter.Formats != null*/
            {

                return new List<MangaFormat>()
                {
                    (MangaFormat) format
                };
            }
            return AllFormats;
        }
    }
}
