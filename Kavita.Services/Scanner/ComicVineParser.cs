using System.IO;
using System.Linq;
using Kavita.API.Services;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Metadata;
using Kavita.Models.Parser;

namespace Kavita.Services.Scanner;
#nullable enable

/// <summary>
/// Responsible for Parsing ComicVine Comics.
/// </summary>
/// <param name="directoryService"></param>
public class ComicVineParser(IDirectoryService directoryService) : DefaultParser(directoryService)
{
    /// <summary>
    /// This Parser generates Series name to be defined as Series + first Issue Volume, so "Batman (2020)".
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="rootPath"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    public override ParserInfo? Parse(string filePath, string rootPath, string libraryRoot, LibraryType type, bool enableMetadata = true, ComicInfo? comicInfo = null)
    {
        if (type != LibraryType.ComicVine) return null;

        var fileName = directoryService.FileSystem.Path.GetFileNameWithoutExtension(filePath);
        // Mylar often outputs cover.jpg, ignore it by default
        if (string.IsNullOrEmpty(fileName) || Scanner.Parser.IsCoverImage(directoryService.FileSystem.Path.GetFileName(filePath))) return null;

        var directoryName = directoryService.FileSystem.DirectoryInfo.New(rootPath).Name;

        var info = new ParserInfo()
        {
            Filename = Path.GetFileName(filePath),
            Format = Scanner.Parser.ParseFormat(filePath),
            Title = Scanner.Parser.RemoveExtensionIfSupported(fileName)!,
            FullFilePath = Scanner.Parser.NormalizePath(filePath),
            Series = string.Empty,
            ComicInfo = comicInfo,
            Chapters = Scanner.Parser.ParseChapter(fileName, type),
            Volumes = Scanner.Parser.ParseVolume(fileName, type)
        };

        // See if we can formulate the name from the ComicInfo
        if (!string.IsNullOrEmpty(info.ComicInfo?.Series) && !string.IsNullOrEmpty(info.ComicInfo?.Volume))
        {
            info.Series = $"{info.ComicInfo.Series} ({info.ComicInfo.Volume})";
        }

        if (string.IsNullOrEmpty(info.Series))
        {
            // Check if we need to fallback to the Folder name AND that the folder matches the format "Series (Year)"
            var directories = directoryService.GetFoldersTillRoot(rootPath, filePath).ToList();
            if (directories.Count > 0)
            {
                foreach (var directory in directories)
                {
                    if (!Scanner.Parser.IsSeriesAndYear(directory)) continue;
                    info.Series = directory;
                    info.Volumes = Scanner.Parser.ParseYear(directory);
                    break;
                }

                // When there was at least one directory and we failed to parse the series, this is the final fallback
                if (string.IsNullOrEmpty(info.Series))
                {
                    info.Series = Scanner.Parser.CleanTitle(directories[0], true);
                }
            }
            else
            {
                if (Scanner.Parser.IsSeriesAndYear(directoryName))
                {
                    info.Series = directoryName;
                    info.Volumes = Scanner.Parser.ParseYear(directoryName);
                }
            }
        }

        // Check if this is a Special/Annual
        info.IsSpecial = Scanner.Parser.IsSpecial(info.Filename, type) || Scanner.Parser.IsSpecial(info.ComicInfo?.Format, type);

        // Patch in other information from ComicInfo
        if (enableMetadata)
        {
            UpdateFromComicInfo(info);
        }

        if (string.IsNullOrEmpty(info.Series))
        {
            info.Series = Scanner.Parser.CleanTitle(directoryName, true);
        }


        return string.IsNullOrEmpty(info.Series) ? null : info;
    }

    /// <summary>
    /// Only applicable for ComicVine library type
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    public override bool IsApplicable(string filePath, LibraryType type)
    {
        return type == LibraryType.ComicVine;
    }

    private new static void UpdateFromComicInfo(ParserInfo info)
    {
        if (info.ComicInfo == null) return;

        if (!string.IsNullOrEmpty(info.ComicInfo.Volume))
        {
            info.Volumes = info.ComicInfo.Volume;
        }
        if (string.IsNullOrEmpty(info.LocalizedSeries) && !string.IsNullOrEmpty(info.ComicInfo.LocalizedSeries))
        {
            info.LocalizedSeries = info.ComicInfo.LocalizedSeries.Trim();
        }
        if (!string.IsNullOrEmpty(info.ComicInfo.Number))
        {
            info.Chapters = info.ComicInfo.Number;
            if (info.IsSpecial && !Scanner.Parser.IsDefaultChapter(info.Chapters))
            {
                info.IsSpecial = false;
                info.Volumes = $"{Scanner.Parser.SpecialVolumeNumber}";
            }
        }

        // Patch is SeriesSort from ComicInfo
        if (!string.IsNullOrEmpty(info.ComicInfo.TitleSort))
        {
            info.SeriesSort = info.ComicInfo.TitleSort.Trim();
        }
    }
}
