using System.IO;
using API.Data.Metadata;
using API.Entities.Enums;

namespace API.Services.Tasks.Scanner.Parser;
#nullable enable

public class ImageParser(IDirectoryService directoryService) : DefaultParser(directoryService)
{
    public override ParserInfo? Parse(string filePath, string rootPath, LibraryType type, ComicInfo? comicInfo = null)
    {
        if (type != LibraryType.Image || !Parser.IsImage(filePath)) return null;

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
            FullFilePath = filePath,
            Title = Parser.RemoveExtensionIfSupported(fileName),
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

    public override bool IsApplicable(string filePath, LibraryType type)
    {
        return type == LibraryType.Image || Parser.IsImage(filePath);
    }
}
