using System;
using System.IO;
using API.Data.Metadata;
using API.Entities.Enums;

namespace API.Services.Tasks.Scanner.Parser;
#nullable enable

/// <summary>
/// This is the basic parser for handling Manga/Comic/Book libraries. This was previously DefaultParser before splitting each parser
/// into their own classes.
/// </summary>
public class BasicParser(IDirectoryService directoryService, IDefaultParser imageParser) : DefaultParser(directoryService)
{
    public override ParserInfo? Parse(string filePath, string rootPath, string libraryRoot, LibraryType type, ComicInfo? comicInfo = null)
    {
        var fileName = directoryService.FileSystem.Path.GetFileNameWithoutExtension(filePath);
        // TODO: Potential Bug: This will return null, but on Image libraries, if all images, we would want to include this.
        if (type != LibraryType.Image && Parser.IsCoverImage(directoryService.FileSystem.Path.GetFileName(filePath))) return null;

        if (Parser.IsImage(filePath))
        {
            return imageParser.Parse(filePath, rootPath, libraryRoot, LibraryType.Image, comicInfo);
        }

        var ret = new ParserInfo()
        {
            Filename = Path.GetFileName(filePath),
            Format = Parser.ParseFormat(filePath),
            Title = Parser.RemoveExtensionIfSupported(fileName)!,
            FullFilePath = Parser.NormalizePath(filePath),
            Series = Parser.ParseSeries(fileName, type),
            ComicInfo = comicInfo,
            Chapters = Parser.ParseChapter(fileName, type),
            Volumes = Parser.ParseVolume(fileName, type),
        };

        if (ret.Series == string.Empty || Parser.IsImage(filePath))
        {
            // Try to parse information out of each folder all the way to rootPath
            ParseFromFallbackFolders(filePath, rootPath, type, ref ret);
        }

        var edition = Parser.ParseEdition(fileName);
        if (!string.IsNullOrEmpty(edition))
        {
            ret.Series = Parser.CleanTitle(ret.Series.Replace(edition, string.Empty), type is LibraryType.Comic);
            ret.Edition = edition;
        }

        var isSpecial = Parser.IsSpecial(fileName, type);
        // We must ensure that we can only parse a special out. As some files will have v20 c171-180+Omake and that
        // could cause a problem as Omake is a special term, but there is valid volume/chapter information.
        if (ret.Chapters == Parser.DefaultChapter && ret.Volumes == Parser.LooseLeafVolume && isSpecial)
        {
            ret.IsSpecial = true;
            ParseFromFallbackFolders(filePath, rootPath, type, ref ret); // NOTE: This can cause some complications, we should try to be a bit less aggressive to fallback to folder
        }

        // If we are a special with marker, we need to ensure we use the correct series name. we can do this by falling back to Folder name
        if (Parser.HasSpecialMarker(fileName))
        {
            ret.IsSpecial = true;
            ret.SpecialIndex = Parser.ParseSpecialIndex(fileName);
            ret.Chapters = Parser.DefaultChapter;
            ret.Volumes = Parser.SpecialVolume;

            // NOTE: This uses rootPath. LibraryRoot works better for manga, but it's not always that way.
            // It might be worth writing some logic if the file is a special, to take the folder above the Specials/
            // if present
            var tempRootPath = rootPath;
            if (rootPath.EndsWith("Specials") || rootPath.EndsWith("Specials/"))
            {
                tempRootPath = rootPath.Replace("Specials", string.Empty).TrimEnd('/');
            }

            // Check if the folder the file exists in is Specials/ and if so, take the parent directory as series (cleaned)
            var fileDirectory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(fileDirectory) &&
                (fileDirectory.EndsWith("Specials", StringComparison.OrdinalIgnoreCase) ||
                 fileDirectory.EndsWith("Specials/", StringComparison.OrdinalIgnoreCase)))
            {
                ret.Series = Parser.CleanTitle(Directory.GetParent(fileDirectory)?.Name ?? string.Empty);
            }
            else
            {
                ParseFromFallbackFolders(filePath, tempRootPath, type, ref ret);
            }

        }

        if (string.IsNullOrEmpty(ret.Series))
        {
            ret.Series = Parser.CleanTitle(fileName, type is LibraryType.Comic);
        }

        // Pdfs may have .pdf in the series name, remove that
        if (Parser.IsPdf(filePath) && ret.Series.ToLower().EndsWith(".pdf"))
        {
            ret.Series = ret.Series.Substring(0, ret.Series.Length - ".pdf".Length);
        }

        // Patch in other information from ComicInfo
        UpdateFromComicInfo(ret);

        if (ret.Volumes == Parser.LooseLeafVolume && ret.Chapters == Parser.DefaultChapter)
        {
            ret.IsSpecial = true;
        }

        // v0.8.x: Introducing a change where Specials will go in a separate Volume with a reserved number
        if (ret.IsSpecial)
        {
            ret.Volumes = Parser.SpecialVolume;
        }

        return ret.Series == string.Empty ? null : ret;
    }

    /// <summary>
    /// Applicable for everything but ComicVine and Image library types
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    public override bool IsApplicable(string filePath, LibraryType type)
    {
        return type != LibraryType.ComicVine && type != LibraryType.Image;
    }
}
