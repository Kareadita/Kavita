﻿using System.IO;
using System.Linq;
using API.Data.Metadata;
using API.Entities.Enums;

namespace API.Services.Tasks.Scanner.Parser;
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
    public override ParserInfo? Parse(string filePath, string rootPath, string libraryRoot, LibraryType type, ComicInfo? comicInfo = null)
    {
        if (type != LibraryType.ComicVine) return null;

        var fileName = directoryService.FileSystem.Path.GetFileNameWithoutExtension(filePath);
        // Mylar often outputs cover.jpg, ignore it by default
        if (string.IsNullOrEmpty(fileName) || Parser.IsCoverImage(directoryService.FileSystem.Path.GetFileName(filePath))) return null;

        var directoryName = directoryService.FileSystem.DirectoryInfo.New(rootPath).Name;

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
                    if (!Parser.IsSeriesAndYear(directory)) continue;
                    info.Series = directory;
                    info.Volumes = Parser.ParseYear(directory);
                    break;
                }

                // When there was at least one directory and we failed to parse the series, this is the final fallback
                if (string.IsNullOrEmpty(info.Series))
                {
                    info.Series = Parser.CleanTitle(directories[0], true, true);
                }
            }
            else
            {
                if (Parser.IsSeriesAndYear(directoryName))
                {
                    info.Series = directoryName;
                    info.Volumes = Parser.ParseYear(directoryName);
                }
            }
        }

        // Check if this is a Special
        info.IsSpecial = Parser.IsComicSpecial(info.Filename) || Parser.IsComicSpecial(info.ComicInfo?.Format);

        // Patch in other information from ComicInfo
        UpdateFromComicInfo(info);

        if (string.IsNullOrEmpty(info.Series))
        {
            info.Series = Parser.CleanTitle(directoryName, true, true);
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
}
