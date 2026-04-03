using System;
using System.Collections.Generic;
using System.IO;
using Kavita.API.Services;
using Kavita.Models.DTOs.Reader;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Metadata;
using Kavita.Models.Parser;
using Kavita.Services.Scanner;
using Microsoft.Extensions.Logging;
using TagLib;

namespace Kavita.Services;

/// <summary>
/// Handles reading metadata from audio files (M4B, MP3, FLAC, etc.) using TagLibSharp.
/// Duration in seconds is stored in the Pages field to reuse the existing progress tracking infrastructure.
/// </summary>
public class AudiobookService(
    ILogger<AudiobookService> logger,
    IDirectoryService directoryService,
    IImageService imageService) : IAudiobookService
{
    /// <summary>
    /// Parses metadata out of an audio file and returns a ParserInfo.
    /// Falls back to filename parsing if metadata is unavailable.
    /// </summary>
    public ParserInfo? ParseInfo(string filePath)
    {
        try
        {
            var fileName = directoryService.FileSystem.Path.GetFileNameWithoutExtension(filePath);
            var info = new ParserInfo
            {
                Filename = Path.GetFileName(filePath),
                Format = MangaFormat.Audio,
                FullFilePath = Parser.NormalizePath(filePath),
                Series = string.Empty
            };

            using var file = TagLib.File.Create(filePath);
            var tag = file.Tag;

            // Use album as series name (standard for audiobooks)
            var album = tag.Album?.Trim();
            var title = tag.Title?.Trim();

            if (!string.IsNullOrEmpty(album))
            {
                info.Series = album;
            }
            else if (!string.IsNullOrEmpty(title))
            {
                info.Series = title;
            }
            else
            {
                info.Series = Parser.CleanTitle(fileName, false);
            }

            // Use track title or filename as chapter title
            info.Title = !string.IsNullOrEmpty(title) ? title : Parser.RemoveExtensionIfSupported(fileName) ?? string.Empty;

            // Map disc number to volume, track number to chapter
            if (tag.Disc > 0)
            {
                info.Volumes = tag.Disc.ToString();
            }

            if (tag.Track > 0)
            {
                info.Chapters = tag.Track.ToString();
            }

            // Extract narrator from Composer tag (common audiobook convention)
            // Authors from Performers
            // We store these in ComicInfo so the existing people processing can handle them
            var comicInfo = new ComicInfo();
            if (tag.Performers?.Length > 0)
            {
                comicInfo.Writer = string.Join(", ", tag.Performers);
            }
            if (tag.Composers?.Length > 0)
            {
                // Store narrators in Penciller since ComicInfo doesn't have a Narrator field.
                // The AudioController maps Penciller -> Narrator for audiobook metadata display.
                comicInfo.Penciller = string.Join(", ", tag.Composers);
            }
            if (tag.Year > 0)
            {
                comicInfo.Year = (int) tag.Year;
            }
            if (!string.IsNullOrEmpty(tag.Comment))
            {
                comicInfo.Summary = tag.Comment.Trim();
            }
            info.ComicInfo = comicInfo;

            return string.IsNullOrEmpty(info.Series) ? null : info;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[AudiobookService] Failed to parse metadata from {FilePath}", filePath);
            // Fallback: parse series name from filename
            var fileName = directoryService.FileSystem.Path.GetFileNameWithoutExtension(filePath);
            return new ParserInfo
            {
                Filename = Path.GetFileName(filePath),
                Format = MangaFormat.Audio,
                FullFilePath = Parser.NormalizePath(filePath),
                Series = Parser.CleanTitle(fileName, false),
                Title = Parser.RemoveExtensionIfSupported(fileName) ?? fileName
            };
        }
    }

    /// <summary>
    /// Returns the duration of an audio file in seconds.
    /// This value is stored in Chapter.Pages to reuse the existing progress tracking infrastructure.
    /// </summary>
    public int GetDurationInSeconds(string filePath)
    {
        try
        {
            using var file = TagLib.File.Create(filePath);
            return (int) Math.Ceiling(file.Properties.Duration.TotalSeconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[AudiobookService] Failed to read duration from {FilePath}", filePath);
            return 0;
        }
    }

    /// <summary>
    /// Extracts the cover image for an audio file.
    /// First tries embedded art in the file, then falls back to cover.jpg/folder.jpg in the same directory.
    /// Returns the filename of the saved cover image, or empty string if none found.
    /// </summary>
    public string GetCoverImage(string filePath, string fileName, string outputDirectory, EncodeFormat encodeFormat, CoverImageSize size = CoverImageSize.Default)
    {
        // 1. Try embedded cover art
        try
        {
            using var file = TagLib.File.Create(filePath);
            var pictures = file.Tag?.Pictures;
            if (pictures != null && pictures.Length > 0)
            {
                // Prefer front cover (type 3), otherwise use first picture
                IPicture? picture = null;
                foreach (var pic in pictures)
                {
                    if (pic.Type == PictureType.FrontCover)
                    {
                        picture = pic;
                        break;
                    }
                }
                picture ??= pictures[0];

                if (!picture.Data.IsEmpty)
                {
                    using var ms = new MemoryStream(picture.Data.Data);
                    return imageService.WriteCoverThumbnail(ms, fileName, outputDirectory, encodeFormat, size);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[AudiobookService] Failed to extract embedded cover from {FilePath}", filePath);
        }

        // 2. Fall back to cover.jpg / folder.jpg in the same directory, then parent directory
        var coverCandidates = new[] { "cover.jpg", "cover.jpeg", "cover.png", "folder.jpg", "folder.jpeg", "folder.png" };
        var directory = Path.GetDirectoryName(filePath);
        var directoriesToSearch = new List<string>();
        if (!string.IsNullOrEmpty(directory)) directoriesToSearch.Add(directory);
        var parent = !string.IsNullOrEmpty(directory) ? Directory.GetParent(directory)?.FullName : null;
        if (!string.IsNullOrEmpty(parent)) directoriesToSearch.Add(parent);

        foreach (var dir in directoriesToSearch)
        {
            foreach (var candidate in coverCandidates)
            {
                var candidatePath = Path.Combine(dir, candidate);
                if (System.IO.File.Exists(candidatePath))
                {
                    try
                    {
                        return imageService.GetCoverImage(candidatePath, fileName, outputDirectory, encodeFormat, size);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "[AudiobookService] Failed to use folder cover image {CoverPath}", candidatePath);
                    }
                }
            }
        }

        return string.Empty;
    }
}
