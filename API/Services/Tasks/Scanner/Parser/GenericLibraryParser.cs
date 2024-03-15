using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using API.Data.Metadata;
using API.Entities.Enums;

namespace API.Services.Tasks.Scanner.Parser;
#nullable enable

/// <summary>
/// Uses an at-runtime array of Regex to parse out information
/// </summary>
/// <param name="directoryService"></param>
public class GenericLibraryParser(IDirectoryService directoryService) : DefaultParser(directoryService)
{
    public override ParserInfo? Parse(string filePath, string rootPath, string libraryRoot, LibraryType type,
        ComicInfo? comicInfo = null, IEnumerable<string>? extraRegex = null)
    {
        if (extraRegex == null) return null;

        // The idea is this is passed in as a default param. Only Generic will use it
        var fileName = directoryService.FileSystem.Path.GetFileNameWithoutExtension(filePath);
        var info = new ParserInfo()
        {
            Filename = Path.GetFileName(filePath),
            Format = Parser.ParseFormat(filePath),
            Title = Parser.RemoveExtensionIfSupported(fileName)!,
            FullFilePath = filePath,
            Series = string.Empty,
            ComicInfo = comicInfo,
            Chapters = Parser.ParseComicChapter(fileName),
            Volumes = Parser.ParseComicVolume(fileName)
        };


        foreach (var regex in extraRegex)
        {
            var matches = new Regex(regex, RegexOptions.IgnoreCase).Matches(fileName);
            foreach (var group in matches.Select(match => match.Groups))
            {
                foreach (var matchKey in group.Keys)
                {
                    var matchValue = group[matchKey].Value;
                    switch (matchKey)
                    {
                        case "Series":
                            info.Series = SetIfNotDefault(matchValue, info.Series);
                            break;
                        case "Chapter":
                            info.Chapters = SetIfNotDefault(matchValue, info.Chapters);
                            break;
                    }
                }
            }
        }

        // Process the final info here: (cleaning values, setting internal encoding overrides)
        if (info.IsSpecial)
        {
            info.Volumes = Parser.SpecialVolume;
        }

        if (string.IsNullOrEmpty(info.Chapters))
        {
            info.Chapters = Parser.DefaultChapter;
        }

        if (!info.IsSpecial && string.IsNullOrEmpty(info.Volumes))
        {
            info.Chapters = Parser.LooseLeafVolume;
        }


        return string.IsNullOrEmpty(info.Series) ? null : info;
    }

    private static string SetIfNotDefault(string value, string originalValue)
    {
        if (string.IsNullOrEmpty(value)) return originalValue;
        if (string.IsNullOrEmpty(originalValue)) return value;

        return originalValue;
    }

    public override bool IsApplicable(string filePath, LibraryType type)
    {
        return type == LibraryType.Generic;
    }
}
