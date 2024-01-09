using System.IO;
using System.Linq;
using API.Entities.Enums;

namespace API.Services.Tasks.Scanner.Parser;
#nullable enable

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
        if (type != LibraryType.Image && Parser.IsCoverImage(_directoryService.FileSystem.Path.GetFileName(filePath))) return null;

        var ret = new ParserInfo()
        {
            Filename = Path.GetFileName(filePath),
            Format = Parser.ParseFormat(filePath),
            Title = Path.GetFileNameWithoutExtension(fileName),
            FullFilePath = filePath,
            Series = string.Empty
        };

        // If library type is Image or this is not a cover image in a non-image library, then use dedicated parsing mechanism
        if (type == LibraryType.Image || Parser.IsImage(filePath))
        {
            // TODO: We can move this up one level
            return ParseImage(filePath, rootPath, ret);
        }


        // This will be called if the epub is already parsed once then we call and merge the information, if the
        if (Parser.IsEpub(filePath))
        {
            ret.Chapters = Parser.ParseChapter(fileName);
            ret.Series = Parser.ParseSeries(fileName);
            ret.Volumes = Parser.ParseVolume(fileName);
        }
        else
        {
            ret.Chapters = type == LibraryType.Comic
                ? Parser.ParseComicChapter(fileName)
                : Parser.ParseChapter(fileName);
            ret.Series = type == LibraryType.Comic ? Parser.ParseComicSeries(fileName) : Parser.ParseSeries(fileName);
            ret.Volumes = type == LibraryType.Comic ? Parser.ParseComicVolume(fileName) : Parser.ParseVolume(fileName);
        }

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

    private ParserInfo ParseImage(string filePath, string rootPath, ParserInfo ret)
    {
        ret.Volumes = Parser.DefaultVolume;
        ret.Chapters = Parser.DefaultChapter;
        var directoryName = _directoryService.FileSystem.DirectoryInfo.New(rootPath).Name;
        ret.Series = directoryName;

        ParseFromFallbackFolders(filePath, rootPath, LibraryType.Image, ref ret);


        if (IsEmptyOrDefault(ret.Volumes, ret.Chapters))
        {
            ret.IsSpecial = true;
        }
        else
        {
            var parsedVolume = Parser.ParseVolume(ret.Filename);
            var parsedChapter = Parser.ParseChapter(ret.Filename);
            if (IsEmptyOrDefault(ret.Volumes, string.Empty) && !parsedVolume.Equals(Parser.DefaultVolume))
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

        return ret;
    }

    private static bool IsEmptyOrDefault(string volumes, string chapters)
    {
        return (string.IsNullOrEmpty(chapters) || chapters == Parser.DefaultChapter) &&
               (string.IsNullOrEmpty(volumes) || volumes == Parser.DefaultVolume);
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
                if ((string.IsNullOrEmpty(ret.Volumes) || ret.Volumes.Equals(Parser.DefaultVolume)) && !string.IsNullOrEmpty(parsedVolume) && !parsedVolume.Equals(Parser.DefaultVolume))
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
}
