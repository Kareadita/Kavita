using System.IO;
using System.Linq;
using API.Entities.Enums;
using API.Services;

namespace API.Parser;

public interface IDefaultParser
{
    ParserInfo Parse(string filePath, string rootPath, LibraryType type = LibraryType.Manga);
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
    public ParserInfo Parse(string filePath, string rootPath, LibraryType type = LibraryType.Manga)
    {
        var fileName = _directoryService.FileSystem.Path.GetFileNameWithoutExtension(filePath);
        ParserInfo ret;

        if (Services.Tasks.Scanner.Parser.Parser.IsEpub(filePath))
        {
            ret = new ParserInfo()
            {
                Chapters = Services.Tasks.Scanner.Parser.Parser.ParseChapter(fileName) ?? Services.Tasks.Scanner.Parser.Parser.ParseComicChapter(fileName),
                Series = Services.Tasks.Scanner.Parser.Parser.ParseSeries(fileName) ?? Services.Tasks.Scanner.Parser.Parser.ParseComicSeries(fileName),
                Volumes = Services.Tasks.Scanner.Parser.Parser.ParseVolume(fileName) ?? Services.Tasks.Scanner.Parser.Parser.ParseComicVolume(fileName),
                Filename = Path.GetFileName(filePath),
                Format = Services.Tasks.Scanner.Parser.Parser.ParseFormat(filePath),
                FullFilePath = filePath
            };
        }
        else
        {
            ret = new ParserInfo()
            {
                Chapters = type == LibraryType.Comic ? Services.Tasks.Scanner.Parser.Parser.ParseComicChapter(fileName) : Services.Tasks.Scanner.Parser.Parser.ParseChapter(fileName),
                Series = type == LibraryType.Comic ? Services.Tasks.Scanner.Parser.Parser.ParseComicSeries(fileName) : Services.Tasks.Scanner.Parser.Parser.ParseSeries(fileName),
                Volumes = type == LibraryType.Comic ? Services.Tasks.Scanner.Parser.Parser.ParseComicVolume(fileName) : Services.Tasks.Scanner.Parser.Parser.ParseVolume(fileName),
                Filename = Path.GetFileName(filePath),
                Format = Services.Tasks.Scanner.Parser.Parser.ParseFormat(filePath),
                Title = Path.GetFileNameWithoutExtension(fileName),
                FullFilePath = filePath
            };
        }

        if (Services.Tasks.Scanner.Parser.Parser.IsImage(filePath) && Services.Tasks.Scanner.Parser.Parser.IsCoverImage(filePath)) return null;

        if (Services.Tasks.Scanner.Parser.Parser.IsImage(filePath))
        {
          // Reset Chapters, Volumes, and Series as images are not good to parse information out of. Better to use folders.
          ret.Volumes = Services.Tasks.Scanner.Parser.Parser.DefaultVolume;
          ret.Chapters = Services.Tasks.Scanner.Parser.Parser.DefaultChapter;
          ret.Series = string.Empty;
        }

        if (ret.Series == string.Empty || Services.Tasks.Scanner.Parser.Parser.IsImage(filePath))
        {
            // Try to parse information out of each folder all the way to rootPath
            ParseFromFallbackFolders(filePath, rootPath, type, ref ret);
        }

        var edition = Services.Tasks.Scanner.Parser.Parser.ParseEdition(fileName);
        if (!string.IsNullOrEmpty(edition))
        {
            ret.Series = Services.Tasks.Scanner.Parser.Parser.CleanTitle(ret.Series.Replace(edition, ""), type is LibraryType.Comic);
            ret.Edition = edition;
        }

        var isSpecial = type == LibraryType.Comic ? Services.Tasks.Scanner.Parser.Parser.IsComicSpecial(fileName) : Services.Tasks.Scanner.Parser.Parser.IsMangaSpecial(fileName);
        // We must ensure that we can only parse a special out. As some files will have v20 c171-180+Omake and that
        // could cause a problem as Omake is a special term, but there is valid volume/chapter information.
        if (ret.Chapters == Services.Tasks.Scanner.Parser.Parser.DefaultChapter && ret.Volumes == Services.Tasks.Scanner.Parser.Parser.DefaultVolume && isSpecial)
        {
            ret.IsSpecial = true;
            ParseFromFallbackFolders(filePath, rootPath, type, ref ret); // NOTE: This can cause some complications, we should try to be a bit less aggressive to fallback to folder
        }

        // If we are a special with marker, we need to ensure we use the correct series name. we can do this by falling back to Folder name
        if (Services.Tasks.Scanner.Parser.Parser.HasSpecialMarker(fileName))
        {
            ret.IsSpecial = true;
            ret.Chapters = Services.Tasks.Scanner.Parser.Parser.DefaultChapter;
            ret.Volumes = Services.Tasks.Scanner.Parser.Parser.DefaultVolume;

            ParseFromFallbackFolders(filePath, rootPath, type, ref ret);
        }

        if (string.IsNullOrEmpty(ret.Series))
        {
            ret.Series = Services.Tasks.Scanner.Parser.Parser.CleanTitle(fileName, type is LibraryType.Comic);
        }

        // Pdfs may have .pdf in the series name, remove that
        if (Services.Tasks.Scanner.Parser.Parser.IsPdf(filePath) && ret.Series.ToLower().EndsWith(".pdf"))
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
      var fallbackFolders = _directoryService.GetFoldersTillRoot(rootPath, filePath).ToList();
        for (var i = 0; i < fallbackFolders.Count; i++)
        {
            var folder = fallbackFolders[i];
            if (Services.Tasks.Scanner.Parser.Parser.IsMangaSpecial(folder)) continue;

            var parsedVolume = type is LibraryType.Manga ? Services.Tasks.Scanner.Parser.Parser.ParseVolume(folder) : Services.Tasks.Scanner.Parser.Parser.ParseComicVolume(folder);
            var parsedChapter = type is LibraryType.Manga ? Services.Tasks.Scanner.Parser.Parser.ParseChapter(folder) : Services.Tasks.Scanner.Parser.Parser.ParseComicChapter(folder);

            if (!parsedVolume.Equals(Services.Tasks.Scanner.Parser.Parser.DefaultVolume) || !parsedChapter.Equals(Services.Tasks.Scanner.Parser.Parser.DefaultChapter))
            {
              if ((string.IsNullOrEmpty(ret.Volumes) || ret.Volumes.Equals(Services.Tasks.Scanner.Parser.Parser.DefaultVolume)) && !parsedVolume.Equals(Services.Tasks.Scanner.Parser.Parser.DefaultVolume))
              {
                ret.Volumes = parsedVolume;
              }
              if ((string.IsNullOrEmpty(ret.Chapters) || ret.Chapters.Equals(Services.Tasks.Scanner.Parser.Parser.DefaultChapter)) && !parsedChapter.Equals(Services.Tasks.Scanner.Parser.Parser.DefaultChapter))
              {
                ret.Chapters = parsedChapter;
              }
            }

            // Generally users group in series folders. Let's try to parse series from the top folder
            if (!folder.Equals(ret.Series) && i == fallbackFolders.Count - 1)
            {
                var series = Services.Tasks.Scanner.Parser.Parser.ParseSeries(folder);

                if (string.IsNullOrEmpty(series))
                {
                    ret.Series = Services.Tasks.Scanner.Parser.Parser.CleanTitle(folder, type is LibraryType.Comic);
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
