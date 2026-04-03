using System.IO;
using Kavita.API.Services;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Metadata;
using Kavita.Models.Parser;

namespace Kavita.Services.Scanner;

/// <summary>
/// Parser for audio files in Audiobook libraries.
/// Delegates metadata extraction to AudiobookService (TagLibSharp).
/// Falls back to filename-based parsing when metadata is unavailable.
/// </summary>
public class AudioParser(IDirectoryService directoryService, IAudiobookService audiobookService) : DefaultParser(directoryService)
{
    public override ParserInfo? Parse(string filePath, string rootPath, string libraryRoot, LibraryType type,
        bool enableMetadata = true, ComicInfo? comicInfo = null)
    {
        ParserInfo? info;

        if (enableMetadata)
        {
            info = audiobookService.ParseInfo(filePath);
            if (info == null) return null;
        }
        else
        {
            var fileName = directoryService.FileSystem.Path.GetFileNameWithoutExtension(filePath);
            info = new ParserInfo
            {
                Filename = Path.GetFileName(filePath),
                Format = MangaFormat.Audio,
                Title = Parser.RemoveExtensionIfSupported(fileName)!,
                FullFilePath = Parser.NormalizePath(filePath),
                Series = Parser.ParseSeries(fileName, type),
                Chapters = Parser.ParseChapter(fileName, type),
                Volumes = Parser.ParseVolume(fileName, type),
                HasEndMarker = Parser.HasEndMarker(fileName)
            };
        }

        // If series is still empty, try to infer from folder structure
        if (string.IsNullOrEmpty(info.Series))
        {
            ParseFromFallbackFolders(filePath, rootPath, type, ref info);
        }

        // Last resort: use filename as series
        if (string.IsNullOrEmpty(info.Series))
        {
            var fileName = directoryService.FileSystem.Path.GetFileNameWithoutExtension(filePath);
            info.Series = Parser.CleanTitle(fileName, false);
        }

        FinalizeNumbers(info);

        return string.IsNullOrEmpty(info.Series) ? null : info;
    }

    /// <summary>
    /// Applicable for any audio file in an Audiobook library.
    /// </summary>
    public override bool IsApplicable(string filePath, LibraryType type)
    {
        return type == LibraryType.Audiobook && Parser.IsAudio(filePath);
    }
}
