using System.IO;
using System.Linq;
using API.Data.Metadata;
using API.Entities.Enums;

namespace API.Services.Tasks.Scanner.Parser;
#nullable enable

public interface IDefaultParser
{
    ParserInfo? Parse(string filePath, string rootPath, LibraryType type, ComicInfo? comicInfo = null);
    void ParseFromFallbackFolders(string filePath, string rootPath, LibraryType type, ref ParserInfo ret);
    bool IsApplicable(string filePath, LibraryType type);
}

/// <summary>
/// This is an implementation of the Parser that is the basis for everything
/// </summary>
public abstract class DefaultParser(IDirectoryService directoryService) : IDefaultParser
{

    /// <summary>
    /// Parses information out of a file path. Can fallback to using directory name if Series couldn't be parsed
    /// from filename.
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="rootPath">Root folder</param>
    /// <param name="type">Allows different Regex to be used for parsing.</param>
    /// <returns><see cref="ParserInfo"/> or null if Series was empty</returns>
    public abstract ParserInfo? Parse(string filePath, string rootPath, LibraryType type, ComicInfo? comicInfo = null);

    /// <summary>
    /// Fills out <see cref="ParserInfo"/> by trying to parse volume, chapters, and series from folders
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="rootPath"></param>
    /// <param name="type"></param>
    /// <param name="ret">Expects a non-null ParserInfo which this method will populate</param>
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

    public abstract bool IsApplicable(string filePath, LibraryType type);

    protected static bool IsEmptyOrDefault(string volumes, string chapters)
    {
        return (string.IsNullOrEmpty(chapters) || chapters == Parser.DefaultChapter) &&
               (string.IsNullOrEmpty(volumes) || volumes == Parser.LooseLeafVolume);
    }
}
