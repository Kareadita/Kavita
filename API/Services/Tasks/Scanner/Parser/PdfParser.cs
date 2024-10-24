using System.IO;
using API.Data.Metadata;
using API.Entities.Enums;

namespace API.Services.Tasks.Scanner.Parser;

public class PdfParser(IDirectoryService directoryService) : DefaultParser(directoryService)
{
    public override ParserInfo Parse(string filePath, string rootPath, string libraryRoot, LibraryType type, ComicInfo comicInfo = null)
    {
        var fileName = directoryService.FileSystem.Path.GetFileNameWithoutExtension(filePath);
        var ret = new ParserInfo
        {
            Filename = Path.GetFileName(filePath),
            Format = Parser.ParseFormat(filePath),
            Title = Parser.RemoveExtensionIfSupported(fileName)!,
            FullFilePath = Parser.NormalizePath(filePath),
            Series = string.Empty,
            ComicInfo = comicInfo,
            Chapters = Parser.ParseChapter(fileName, type)
        };

        if (type == LibraryType.Book)
        {
            ret.Chapters = Parser.DefaultChapter;
        }

        ret.Series = Parser.ParseSeries(fileName, type);
        ret.Volumes = Parser.ParseVolume(fileName, type);

        if (ret.Series == string.Empty)
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
            // NOTE: This can cause some complications, we should try to be a bit less aggressive to fallback to folder
            ParseFromFallbackFolders(filePath, rootPath, type, ref ret);
        }

        // If we are a special with marker, we need to ensure we use the correct series name. we can do this by falling back to Folder name
        if (Parser.HasSpecialMarker(fileName))
        {
            ret.IsSpecial = true;
            ret.SpecialIndex = Parser.ParseSpecialIndex(fileName);
            ret.Chapters = Parser.DefaultChapter;
            ret.Volumes = Parser.SpecialVolume;

            var tempRootPath = rootPath;
            if (rootPath.EndsWith("Specials") || rootPath.EndsWith("Specials/"))
            {
                tempRootPath = rootPath.Replace("Specials", string.Empty).TrimEnd('/');
            }

            ParseFromFallbackFolders(filePath, tempRootPath, type, ref ret);
        }

        // Patch in other information from ComicInfo
        UpdateFromComicInfo(ret);

        if (ret.Chapters == Parser.DefaultChapter && ret.Volumes == Parser.LooseLeafVolume && type == LibraryType.Book)
        {
            ret.IsSpecial = true;
            ret.Chapters = Parser.DefaultChapter;
            ret.Volumes = Parser.SpecialVolume;
            ParseFromFallbackFolders(filePath, rootPath, type, ref ret);
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

        // v0.8.x: Introducing a change where Specials will go in a separate Volume with a reserved number
        if (ret.IsSpecial)
        {
            ret.Volumes = $"{Parser.SpecialVolumeNumber}";
        }

        return string.IsNullOrEmpty(ret.Series) ? null : ret;
    }

    /// <summary>
    /// Only applicable for PDF files
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    public override bool IsApplicable(string filePath, LibraryType type)
    {
        return Parser.IsPdf(filePath);
    }
}
