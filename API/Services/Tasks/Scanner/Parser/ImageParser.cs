using System.IO;
using API.Data.Metadata;
using API.Entities.Enums;

namespace API.Services.Tasks.Scanner.Parser;
#nullable enable

public class ImageParser(IDirectoryService directoryService) : DefaultParser(directoryService)
{
    public override ParserInfo? Parse(string filePath, string rootPath, string libraryRoot, LibraryType type, ComicInfo? comicInfo = null)
    {
        if (!IsApplicable(filePath, type)) return null;

        var directoryName = directoryService.FileSystem.DirectoryInfo.New(rootPath).Name;
        var fileName = directoryService.FileSystem.Path.GetFileNameWithoutExtension(filePath);
        var ret = new ParserInfo
        {
            Series = directoryName,
            Volumes = Parser.LooseLeafVolume,
            Chapters = Parser.DefaultChapter,
            ComicInfo = comicInfo,
            Format = MangaFormat.Image,
            Filename = Path.GetFileName(filePath),
            FullFilePath = Parser.NormalizePath(filePath),
            Title = fileName,
        };
        ParseFromFallbackFolders(filePath, libraryRoot, LibraryType.Image, ref ret);

        if (IsEmptyOrDefault(ret.Volumes, ret.Chapters))
        {
            ret.IsSpecial = true;
            ret.Volumes = Parser.SpecialVolume;
        }

        // Override the series name, as fallback folders needs it to try and parse folder name
        if (string.IsNullOrEmpty(ret.Series) || ret.Series.Equals(directoryName))
        {
            ret.Series = Parser.CleanTitle(directoryName);
        }


        return string.IsNullOrEmpty(ret.Series) ? null : ret;
    }

    /// <summary>
    /// Only applicable for Image files and Image library type
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    public override bool IsApplicable(string filePath, LibraryType type)
    {
        return type == LibraryType.Image && Parser.IsImage(filePath);
    }
}
