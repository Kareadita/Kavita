using System.IO;
using System.Linq;
using API.Entities.Enums;
using API.Parser;

namespace API.Services.Tasks.Scanner.Parser;

public interface IDefaultParser
{
    ParserInfo? Parse(string filePath, string rootPath, LibraryType type = LibraryType.Manga);
    void ParseFromFallbackFolders(string filePath, string rootPath, LibraryType type, ref ParserInfo ret);
}

/// <summary>
/// This is an implementation of the Parser that is the basis for everything
/// </summary>
public class DefaultParser : IDefaultParser
{
    private readonly IDirectoryService _directoryService;

    public DefaultParser(IDirectoryService directoryService)
    {
        _directoryService = directoryService;
    }

    /// <summary>
    /// Parses information out of a file path. Will fallback to using directory name if Series couldn't be parsed
    /// from filename.
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="rootPath">Root folder</param>
    /// <param name="type">Defaults to Manga. Allows different Regex to be used for parsing.</param>
    /// <returns><see cref="ParserInfo"/> or null if Series was empty</returns>
    public ParserInfo? Parse(string filePath, string rootPath, LibraryType type = LibraryType.Manga)
    {
        var fileName = _directoryService.FileSystem.Path.GetFileNameWithoutExtension(filePath);
        // TODO: Potential Bug: This will return null, but on Image libraries, if all images, we would want to include this.
        if (Parser.IsCoverImage(_directoryService.FileSystem.Path.GetFileName(filePath))) return null;
        ParserInfo ret;

        if (Parser.IsEpub(filePath))
        {
            ret = new ParserInfo
            {
                Chapters = Parser.ParseChapter(fileName) ?? Parser.ParseComicChapter(fileName),
                Series = Parser.ParseSeries(fileName) ?? Parser.ParseComicSeries(fileName),
                Volumes = Parser.ParseVolume(fileName) ?? Parser.ParseComicVolume(fileName),
                Filename = Path.GetFileName(filePath),
                Format = Parser.ParseFormat(filePath),
                FullFilePath = filePath
            };
        }
        else
        {
            ret = new ParserInfo
            {
                Chapters = type == LibraryType.Comic ? Parser.ParseComicChapter(fileName) : Parser.ParseChapter(fileName),
                Series = type == LibraryType.Comic ? Parser.ParseComicSeries(fileName) : Parser.ParseSeries(fileName),
                Volumes = type == LibraryType.Comic ? Parser.ParseComicVolume(fileName) : Parser.ParseVolume(fileName),
                Filename = Path.GetFileName(filePath),
                Format = Parser.ParseFormat(filePath),
                Title = Path.GetFileNameWithoutExtension(fileName),
                FullFilePath = filePath
            };
        }


        if (Parser.IsImage(filePath))
        {
          // Reset Chapters, Volumes, and Series as images are not good to parse information out of. Better to use folders.
          ret.Volumes = Parser.DefaultVolume;
          ret.Chapters = Parser.DefaultChapter;
          ret.Series = string.Empty;
        }

        if (ret.Series == string.Empty || Parser.IsImage(filePath))
        {
            // Try to parse information out of each folder all the way to rootPath
            ParseFromFallbackFolders(filePath, rootPath, type, ref ret);
        }

        var edition = Parser.ParseEdition(fileName);
        if (!string.IsNullOrEmpty(edition))
        {
            ret.Series = Parser.CleanTitle(ret.Series.Replace(edition, ""), type is LibraryType.Comic);
            ret.Edition = edition;
        }

        var isSpecial = type == LibraryType.Comic ? Parser.IsComicSpecial(fileName) : Parser.IsMangaSpecial(fileName);
        // We must ensure that we can only parse a special out. As some files will have v20 c171-180+Omake and that
        // could cause a problem as Omake is a special term, but there is valid volume/chapter information.
        if (ret.Chapters == Parser.DefaultChapter && ret.Volumes == Parser.DefaultVolume && isSpecial)
        {
            ret.IsSpecial = true;
            ParseFromFallbackFolders(filePath, rootPath, type, ref ret); // NOTE: This can cause some complications, we should try to be a bit less aggressive to fallback to folder
        }

        // If we are a special with marker, we need to ensure we use the correct series name. we can do this by falling back to Folder name
        if (Parser.HasSpecialMarker(fileName))
        {
            ret.IsSpecial = true;
            ret.Chapters = Parser.DefaultChapter;
            ret.Volumes = Parser.DefaultVolume;

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

        return ret.Series == string.Empty ? null : ret;
    }

    /// <summary>
    /// Fills out <see cref="ParserInfo"/> by trying to parse volume, chapters, and series from folders
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="rootPath"></param>
    /// <param name="type"></param>
    /// <param name="ret">Expects a non-null ParserInfo which this method will populate</param>
    public void ParseFromFallbackFolders(string filePath, string rootPath, LibraryType type, ref ParserInfo ret)
    {
        var fallbackFolders = _directoryService.GetFoldersTillRoot(rootPath, filePath)
            .Where(f => !Parser.IsMangaSpecial(f))
            .ToList();

        if (fallbackFolders.Count == 0)
        {
            var rootFolderName = _directoryService.FileSystem.DirectoryInfo.New(rootPath).Name;
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

            if (!parsedVolume.Equals(Parser.DefaultVolume) || !parsedChapter.Equals(Parser.DefaultChapter))
            {
                if ((string.IsNullOrEmpty(ret.Volumes) || ret.Volumes.Equals(Parser.DefaultVolume)) && !parsedVolume.Equals(Parser.DefaultVolume))
                {
                    ret.Volumes = parsedVolume;
                }
                if ((string.IsNullOrEmpty(ret.Chapters) || ret.Chapters.Equals(Parser.DefaultChapter)) && !parsedChapter.Equals(Parser.DefaultChapter))
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

                if (!string.IsNullOrEmpty(series) && (string.IsNullOrEmpty(ret.Series) || !folder.Contains(ret.Series)))
                {
                    ret.Series = series;
                    break;
                }
            }
        }
    }
}
