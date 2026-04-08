using System.Linq;
using Kavita.API.Services;
using Kavita.Common.Helpers;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Metadata;
using Kavita.Models.Parser;

namespace Kavita.Services.Scanner;

public interface IDefaultParser
{
    ParserInfo? Parse(string filePath, string rootPath, string libraryRoot, LibraryType type, bool enableMetadata = true, ComicInfo? comicInfo = null);
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
    /// <param name="libraryRoot"></param>
    /// <param name="type">Allows different Regex to be used for parsing.</param>
    /// <param name="enableMetadata">Allows overriding data from metadata (ComicInfo/pdf/epub)</param>
    /// <param name="comicInfo"></param>
    /// <returns><see cref="ParserInfo"/> or null if Series was empty</returns>
    public abstract ParserInfo? Parse(string filePath, string rootPath, string libraryRoot, LibraryType type, bool enableMetadata = true, ComicInfo? comicInfo = null);

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

            var isLooseLeafVolume = Parser.IsLooseLeafVolume(parsedVolume);
            var isDefaultChapter = Parser.IsDefaultChapter(parsedChapter);

            if ((string.IsNullOrEmpty(ret.Volumes) || Parser.IsLooseLeafVolume(ret.Volumes))
                && !string.IsNullOrEmpty(parsedVolume) && !isLooseLeafVolume)
            {
                ret.Volumes = parsedVolume;
            }
            if ((string.IsNullOrEmpty(ret.Chapters) || ret.Chapters.Equals(Parser.DefaultChapter))
                && !string.IsNullOrEmpty(parsedChapter) && !isDefaultChapter)
            {
                ret.Chapters = parsedChapter;
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
        if (!string.IsNullOrEmpty(info.ComicInfo.SeriesSort))
        {
            info.SeriesSort = info.ComicInfo.SeriesSort.Trim();
        }

    }

    public abstract bool IsApplicable(string filePath, LibraryType type);

    protected static bool IsEmptyOrDefault(string volumes, string chapters)
    {
        return (string.IsNullOrEmpty(chapters) || Parser.IsDefaultChapter(chapters)) &&
               (string.IsNullOrEmpty(volumes) || Parser.IsLooseLeafVolume(volumes));
    }

    /// <summary>
    /// Attempts to fill in as much information as possible from Notes then Weblinks fields
    /// for different metadata Ids
    /// </summary>
    /// <param name="info"></param>
    public static void ParseExternalIdsFromNotesAndWeblinks(ParserInfo info)
    {
        var notes = info.ComicInfo?.Notes;
        var weblinks = info.ComicInfo?.Web;

        info.AniListId = WeblinkParser.GetAniListId(weblinks);
        info.MalId = WeblinkParser.GetMalId(weblinks);

        var comicvineId = Parser.ParseComicVineIdFromComicInfoNote(notes);
        var parsedCvWeblink = WeblinkParser.GetComicVineId(weblinks);
        info.ComicVineId = comicvineId;

        // If we have a seriesId, set it. Otherwise, we set the issue id
        if (parsedCvWeblink.Item2)
        {
            info.ComicVineSeriesId = parsedCvWeblink.Item1;
        } else if (string.IsNullOrEmpty(comicvineId))
        {
            info.ComicVineId ??= parsedCvWeblink.Item1;
        }

        var metronId = Parser.ParseMetronIdFromComicInfoNote(notes);
        info.MetronId = !string.IsNullOrEmpty(metronId)
            ? long.Parse(metronId)
            : 0L;
    }

    /// <summary>
    /// Set the Highest Volume/Chapter numbers for utilization in the rest of the pipeline.
    /// </summary>
    /// <remarks>This ensures total counts work, and we avoid rechurning the strings </remarks>
    /// <param name="info"></param>
    protected static void FinalizeNumbers(ParserInfo info)
    {
        info.HighestChapter = Parser.MaxNumberFromRange(info.Chapters);
        info.LowestChapter = Parser.MinNumberFromRange(info.Chapters);
        info.HighestVolume = Parser.MaxNumberFromRange(info.Volumes);
        info.LowestVolume = Parser.MinNumberFromRange(info.Volumes);
    }
}
