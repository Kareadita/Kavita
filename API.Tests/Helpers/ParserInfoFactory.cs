using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using API.Entities.Enums;
using API.Extensions;
using API.Services.Tasks.Scanner;
using API.Services.Tasks.Scanner.Parser;

namespace API.Tests.Helpers;

public static class ParserInfoFactory
{
    public static ParserInfo CreateParsedInfo(string series, string volumes, string chapters, string filename, bool isSpecial)
    {
        return new ParserInfo()
        {
            Chapters = chapters,
            Edition = "",
            Format = MangaFormat.Archive,
            FullFilePath = Path.Join(@"/manga/", filename),
            Filename = filename,
            IsSpecial = isSpecial,
            Title = Path.GetFileNameWithoutExtension(filename),
            Series = series,
            Volumes = volumes
        };
    }

    public static void AddToParsedInfo(IDictionary<ParsedSeries, IList<ParserInfo>> collectedSeries, ParserInfo info)
    {
        var existingKey = collectedSeries.Keys.FirstOrDefault(ps =>
            ps.Format == info.Format && ps.NormalizedName == info.Series.ToNormalized());
        existingKey ??= new ParsedSeries()
        {
            Format = info.Format,
            Name = info.Series,
            NormalizedName = info.Series.ToNormalized()
        };
        if (collectedSeries.GetType() == typeof(ConcurrentDictionary<,>))
        {
            ((ConcurrentDictionary<ParsedSeries, IList<ParserInfo>>) collectedSeries).AddOrUpdate(existingKey, new List<ParserInfo>() {info}, (_, oldValue) =>
            {
                oldValue ??= new List<ParserInfo>();
                if (!oldValue.Contains(info))
                {
                    oldValue.Add(info);
                }

                return oldValue;
            });
        }
        else
        {
            if (!collectedSeries.ContainsKey(existingKey))
            {
                collectedSeries.Add(existingKey, new List<ParserInfo>() {info});
            }
            else
            {
                var list = collectedSeries[existingKey];
                if (!list.Contains(info))
                {
                    list.Add(info);
                }

                collectedSeries[existingKey] = list;
            }

        }

    }
}
