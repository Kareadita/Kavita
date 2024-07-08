using System.IO;
using System.Linq;
using API.Data.Metadata;
using API.Entities.Enums;

namespace API.Services.Tasks.Scanner.Parser;
#nullable enable

public interface IDefaultParser
{
    ParserInfo? Parse(string filePath, string rootPath, string libraryRoot, LibraryType type, ComicInfo? comicInfo = null);
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
    public abstract ParserInfo? Parse(string filePath, string rootPath, string libraryRoot, LibraryType type, ComicInfo? comicInfo = null);

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
            .Where(f => !Parser.IsSpecial(f, type))
            .ToList();

        if (fallbackFolders.Count == 0)
        {
            var rootFolderName = directoryService.FileSystem.DirectoryInfo.New(rootPath).Name;
            var series = Parser.ParseSeries(rootFolderName, type);

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

            var parsedVolume = Parser.ParseVolume(folder, type);
            var parsedChapter = Parser.ParseChapter(folder, type);

            if (!parsedVolume.Equals(Parser.LooseLeafVolume) || !parsedChapter.Equals(Parser.DefaultChapter))
            {
                if ((string.IsNullOrEmpty(ret.Volumes) || ret.Volumes.Equals(Parser.LooseLeafVolume))
                    && !string.IsNullOrEmpty(parsedVolume) && !parsedVolume.Equals(Parser.LooseLeafVolume))
                {
                    ret.Volumes = parsedVolume;
                }
                if ((string.IsNullOrEmpty(ret.Chapters) || ret.Chapters.Equals(Parser.DefaultChapter))
                    && !string.IsNullOrEmpty(parsedChapter) && !parsedChapter.Equals(Parser.DefaultChapter))
                {
                    ret.Chapters = parsedChapter;
                }
            }

            // Generally users group in series folders. Let's try to parse series from the top folder
            if (!folder.Equals(ret.Series) && i == fallbackFolders.Count - 1)
            {
                var series = Parser.ParseSeries(folder, type);

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

    protected static void UpdateFromComicInfo(ParserInfo info)
    {
        if (info.ComicInfo == null) return;

        if (!string.IsNullOrEmpty(info.ComicInfo.Volume))
        {
            info.Volumes = info.ComicInfo.Volume;
        }
        if (!string.IsNullOrEmpty(info.ComicInfo.Number))
        {
            info.Chapters = info.ComicInfo.Number;
        }
        if (!string.IsNullOrEmpty(info.ComicInfo.Series))
        {
            info.Series = info.ComicInfo.Series.Trim();
        }
        if (!string.IsNullOrEmpty(info.ComicInfo.LocalizedSeries))
        {
            info.LocalizedSeries = info.ComicInfo.LocalizedSeries.Trim();
        }

        if (!string.IsNullOrEmpty(info.ComicInfo.Format) && Parser.HasComicInfoSpecial(info.ComicInfo.Format))
        {
            info.IsSpecial = true;
            info.Chapters = Parser.DefaultChapter;
            info.Volumes = Parser.SpecialVolume;
        }

        // Patch is SeriesSort from ComicInfo
        if (!string.IsNullOrEmpty(info.ComicInfo.TitleSort))
        {
            info.SeriesSort = info.ComicInfo.TitleSort.Trim();
        }

    }

    public abstract bool IsApplicable(string filePath, LibraryType type);

    protected static bool IsEmptyOrDefault(string volumes, string chapters)
    {
        return (string.IsNullOrEmpty(chapters) || chapters == Parser.DefaultChapter) &&
               (string.IsNullOrEmpty(volumes) || volumes == Parser.LooseLeafVolume);
    }
}
