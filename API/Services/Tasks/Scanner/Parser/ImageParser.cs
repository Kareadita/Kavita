using System.Linq;
using API.Data.Metadata;
using API.Entities.Enums;

namespace API.Services.Tasks.Scanner.Parser;
#nullable enable

public class ImageParser(IDirectoryService directoryService) : IDefaultParser
{
    public ParserInfo? Parse(string filePath, string rootPath, LibraryType type, ComicInfo? comicInfo = null)
    {
        if (type != LibraryType.Image || !Parser.IsImage(filePath)) return null;

        var directoryName = directoryService.FileSystem.DirectoryInfo.New(rootPath).Name;
        var ret = new ParserInfo
        {
            Series = directoryName,
            Volumes = Parser.LooseLeafVolume,
            Chapters = Parser.DefaultChapter,
            ComicInfo = comicInfo
        };

        ParseFromFallbackFolders(filePath, rootPath, LibraryType.Image, ref ret);


        if (IsEmptyOrDefault(ret.Volumes, ret.Chapters))
        {
            ret.IsSpecial = true;
        }
        else
        {
            var parsedVolume = Parser.ParseVolume(ret.Filename);
            var parsedChapter = Parser.ParseChapter(ret.Filename);
            if (IsEmptyOrDefault(ret.Volumes, string.Empty) && !parsedVolume.Equals(Parser.LooseLeafVolume))
            {
                ret.Volumes = parsedVolume;
            }
            if (IsEmptyOrDefault(string.Empty, ret.Chapters) && !parsedChapter.Equals(Parser.DefaultChapter))
            {
                ret.Chapters = parsedChapter;
            }
        }


        // Override the series name, as fallback folders needs it to try and parse folder name
        if (string.IsNullOrEmpty(ret.Series) || ret.Series.Equals(directoryName))
        {
            ret.Series = Parser.CleanTitle(directoryName, replaceSpecials: false);
        }

        return string.IsNullOrEmpty(ret.Series) ? null : ret;
    }

    public void ParseFromFallbackFolders(string filePath, string rootPath, LibraryType type, ref ParserInfo ret)
    {
        var fallbackFolders = directoryService.GetFoldersTillRoot(rootPath, filePath)
            .Where(f => !Parser.IsMangaSpecial(f))
            .ToList();

        if (fallbackFolders.Count == 0)
        {
            var rootFolderName = directoryService.FileSystem.DirectoryInfo.New(rootPath).Name;
            var series = Parser.ParseSeries(rootFolderName);

            if (string.IsNullOrEmpty(series))
            {
                ret.Series = Parser.CleanTitle(rootFolderName, type is LibraryType.Comic);
                return;
            }

            if (!string.IsNullOrEmpty(series) && (string.IsNullOrEmpty(ret.Series) || !rootFolderName.Contains(ret.Series)))
            {
                ret.Series = series;
                return;
            }
        }

        for (var i = 0; i < fallbackFolders.Count; i++)
        {
            var folder = fallbackFolders[i];

            var parsedVolume = type is LibraryType.Manga ? Parser.ParseVolume(folder) : Parser.ParseComicVolume(folder);
            var parsedChapter = type is LibraryType.Manga ? Parser.ParseChapter(folder) : Parser.ParseComicChapter(folder);

            if (!parsedVolume.Equals(Parser.LooseLeafVolume) || !parsedChapter.Equals(Parser.DefaultChapter))
            {
                if ((string.IsNullOrEmpty(ret.Volumes) || ret.Volumes.Equals(Parser.LooseLeafVolume)) && !string.IsNullOrEmpty(parsedVolume) && !parsedVolume.Equals(Parser.LooseLeafVolume))
                {
                    ret.Volumes = parsedVolume;
                }
                if ((string.IsNullOrEmpty(ret.Chapters) || ret.Chapters.Equals(Parser.DefaultChapter)) && !string.IsNullOrEmpty(parsedChapter) && !parsedChapter.Equals(Parser.DefaultChapter))
                {
                    ret.Chapters = parsedChapter;
                }
            }

            // Generally users group in series folders. Let's try to parse series from the top folder
            if (!folder.Equals(ret.Series) && i == fallbackFolders.Count - 1)
            {
                var series = Parser.ParseSeries(folder);

                if (string.IsNullOrEmpty(series))
                {
                    ret.Series = Parser.CleanTitle(folder, type is LibraryType.Comic);
                    break;
                }

                if (!string.IsNullOrEmpty(series) && (string.IsNullOrEmpty(ret.Series) && !folder.Contains(ret.Series)))
                {
                    ret.Series = series;
                    break;
                }
            }
        }
    }

    public bool IsApplicable(string filePath, LibraryType type)
    {
        return type == LibraryType.Image || Parser.IsImage(filePath);
    }

    private static bool IsEmptyOrDefault(string volumes, string chapters)
    {
        return (string.IsNullOrEmpty(chapters) || chapters == Parser.DefaultChapter) &&
               (string.IsNullOrEmpty(volumes) || volumes == Parser.LooseLeafVolume);
    }
}
