using System.Collections.Generic;
using API.Entities;

namespace API.Tests.Helpers;

/// <summary>
/// Used to help quickly create DB entities for Unit Testing
/// </summary>
public static class EntityFactory
{
    public static Chapter CreateChapter(string range, bool isSpecial, List<MangaFile> files = null, int pageCount = 0, string title = null)
    {
        return new Chapter()
        {
            IsSpecial = isSpecial,
            Range = range,
            Number = API.Services.Tasks.Scanner.Parser.Parser.MinNumberFromRange(range) + string.Empty,
            Files = files ?? new List<MangaFile>(),
            Pages = pageCount,
            Title = title ?? range
        };
    }
}



