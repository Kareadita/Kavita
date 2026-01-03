using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.Entities;
using API.Entities.Enums;
using API.Services.Tasks.Scanner.Parser;

namespace API.Services;
#nullable enable

public interface IEntityDisplayService
{
    Task<(string displayName, bool neededRename)> GetVolumeDisplayName( VolumeDto volume, int userId, EntityDisplayOptions options);
    Task<string> GetChapterDisplayName(ChapterDto chapter, int userId, EntityDisplayOptions options);
    Task<string> GetChapterDisplayName(Chapter chapter, int userId, EntityDisplayOptions options);
    Task<string> GetEntityDisplayName(ChapterDto chapter, int userId, EntityDisplayOptions options);

}


/// <summary>
/// Service responsible for generating user-friendly display names for Volumes and Chapters.
/// Centralizes naming logic to avoid exposing internal encodings (-100000).
/// </summary>
public class EntityDisplayService(ILocalizationService localizationService, IUnitOfWork unitOfWork) : IEntityDisplayService
{
    /// <summary>
    /// Generates a user-friendly display name for a Volume.
    /// </summary>
    /// <param name="volume">The volume to generate a name for</param>
    /// <param name="userId">User ID for localization</param>
    /// <param name="options">Display options</param>
    /// <returns>Tuple of (displayName, neededRename) where neededRename indicates if the volume was modified</returns>
    public async Task<(string displayName, bool neededRename)> GetVolumeDisplayName( VolumeDto volume, int userId, EntityDisplayOptions options)
    {
        // Handle special volumes - these shouldn't be displayed as regular volumes
        if (volume.IsSpecial() || volume.IsLooseLeaf())
        {
            return (string.Empty, false);
        }

        var libraryType = options.LibraryType;
        var neededRename = false;

        // Book/LightNovel treatment - use chapter title as volume name
        if (libraryType is LibraryType.Book or LibraryType.LightNovel)
        {
            var firstChapter = volume.Chapters.FirstOrDefault();
            if (firstChapter == null)
            {
                return (string.Empty, false);
            }

            // Skip special chapters
            if (firstChapter.IsSpecial)
            {
                return (string.Empty, false);
            }

            // Use chapter's title name if available
            if (!string.IsNullOrEmpty(firstChapter.TitleName))
            {
                neededRename = true;
                return (firstChapter.TitleName, neededRename);
            }

            // Fallback: extract from Range if it's not a loose-leaf marker
            if (!Parser.IsLooseLeafVolume(firstChapter.Range))
            {
                var title = Path.GetFileNameWithoutExtension(firstChapter.Range);
                if (!string.IsNullOrEmpty(title))
                {
                    neededRename = true;
                    var displayName = string.IsNullOrEmpty(volume.Name)
                        ? title
                        : $"{volume.Name} - {title}";
                    return (displayName, neededRename);
                }
            }

            return (string.Empty, false);
        }

        // Standard volume naming for Comics/Manga
        if (options.IncludePrefix)
        {
            var volumeLabel = options.VolumePrefix
                ?? await localizationService.Translate(userId, "volume-num", string.Empty);
            neededRename = true;
            return ($"{volumeLabel.Trim()} {volume.Name}".Trim(), neededRename);
        }

        return (volume.Name, neededRename);
    }

    /// <summary>
    /// Generates a user-friendly display name for a Chapter (DTO).
    /// </summary>
    public async Task<string> GetChapterDisplayName( ChapterDto chapter, int userId, EntityDisplayOptions options)
    {
        return await GetChapterDisplayNameCore(
            chapter.IsSpecial,
            chapter.Range,
            chapter.Title,
            userId,
            options);
    }

    /// <summary>
    /// Generates a user-friendly display name for a Chapter (Entity).
    /// </summary>
    public async Task<string> GetChapterDisplayName( Chapter chapter, int userId, EntityDisplayOptions options)
    {
        return await GetChapterDisplayNameCore(
            chapter.IsSpecial,
            chapter.Range,
            chapter.Title,
            userId,
            options);
    }

    /// <summary>
    /// Smart method that generates display name for a chapter, automatically detecting if it needs
    /// to fetch the volume name instead (for loose-leaf volumes in book libraries).
    /// This is the recommended method for most scenarios as it handles internal encodings.
    /// </summary>
    /// <param name="chapter">The chapter to generate a name for</param>
    /// <param name="userId">User ID for localization</param>
    /// <param name="options">Display options</param>
    /// <returns>User-friendly display name</returns>
    public async Task<string> GetEntityDisplayName( ChapterDto chapter, int userId, EntityDisplayOptions options)
    {
        // Detect if this is a loose-leaf volume that should be displayed as a volume name
        if (Parser.IsLooseLeafVolume(chapter.Title))
        {
            var volume = await unitOfWork.VolumeRepository.GetVolumeDtoAsync(chapter.VolumeId, userId);
            if (volume != null)
            {
                var (label, _) = await GetVolumeDisplayName(volume, userId, options);
                if (!string.IsNullOrEmpty(label))
                {
                    return label;
                }
            }
        }

        // Standard chapter display
        return await GetChapterDisplayName(chapter, userId, options);
    }


    /// <summary>
    /// Core implementation for chapter display name generation.
    /// </summary>
    private async Task<string> GetChapterDisplayNameCore( bool isSpecial, string range, string? title, int userId, EntityDisplayOptions options)
    {
        // Handle special chapters - use cleaned title or fallback to range
        if (isSpecial)
        {
            if (!string.IsNullOrEmpty(title))
            {
                return Parser.CleanSpecialTitle(title);
            }
            // Fallback to cleaned range (filename)
            return Parser.CleanSpecialTitle(range);
        }

        var libraryType = options.LibraryType;
        var useHash = ShouldUseHashSymbol(libraryType, options.ForceHashSymbol);
        var hashSpot = useHash ? "#" : string.Empty;

        // Generate base chapter name based on library type
        var baseChapter = libraryType switch
        {
            LibraryType.Book => await localizationService.Translate(userId, "book-num", title ?? range),
            LibraryType.LightNovel => await localizationService.Translate(userId, "book-num", range),
            LibraryType.Comic => await localizationService.Translate(userId, "issue-num", hashSpot, range),
            LibraryType.ComicVine => await localizationService.Translate(userId, "issue-num", hashSpot, range),
            LibraryType.Manga => await localizationService.Translate(userId, "chapter-num", range),
            LibraryType.Image => await localizationService.Translate(userId, "chapter-num", range),
            _ => await localizationService.Translate(userId, "chapter-num", range)
        };

        // Append title suffix if requested and title differs from range
        if (options.IncludeTitleSuffix &&
            !string.IsNullOrEmpty(title) &&
            libraryType != LibraryType.Book &&
            title != range)
        {
            baseChapter += $" - {title}";
        }

        return baseChapter;
    }

    /// <summary>
    /// Determines if hash symbol should be used based on library type and override.
    /// </summary>
    private static bool ShouldUseHashSymbol(LibraryType libraryType, bool? forceHashSymbol)
    {
        if (forceHashSymbol.HasValue)
        {
            return forceHashSymbol.Value;
        }

        // Smart default: Comics use hash
        return libraryType is LibraryType.Comic or LibraryType.ComicVine;
    }
}

/// <summary>
/// Options for controlling entity display name generation.
/// </summary>
public class EntityDisplayOptions
{
    /// <summary>
    /// The library type context for the entity.
    /// </summary>
    public LibraryType LibraryType { get; set; }

    /// <summary>
    /// Whether to append the chapter title as a suffix (e.g., "Chapter 5 - The Beginning").
    /// Default: true
    /// </summary>
    public bool IncludeTitleSuffix { get; set; } = true;

    /// <summary>
    /// Force inclusion or exclusion of hash symbol (#) for issues.
    /// If null, smart default based on library type is used.
    /// </summary>
    public bool? ForceHashSymbol { get; set; } = null;

    /// <summary>
    /// Whether to include the volume prefix (e.g., "Volume 1" vs "1").
    /// Default: true
    /// </summary>
    public bool IncludePrefix { get; set; } = true;

    /// <summary>
    /// Pre-translated volume prefix to avoid redundant localization calls.
    /// If null, will be fetched via localization service.
    /// </summary>
    public string? VolumePrefix { get; set; } = null;

    /// <summary>
    /// Creates default options for a given library type.
    /// </summary>
    public static EntityDisplayOptions Default(LibraryType libraryType) => new()
    {
        LibraryType = libraryType
    };

    /// <summary>
    /// Creates options with title suffix disabled (useful for compact displays).
    /// </summary>
    public static EntityDisplayOptions WithoutTitleSuffix(LibraryType libraryType) => new()
    {
        LibraryType = libraryType,
        IncludeTitleSuffix = false
    };

    /// <summary>
    /// Creates options without prefix (e.g., returns "5" instead of "Volume 5").
    /// </summary>
    public static EntityDisplayOptions WithoutPrefix(LibraryType libraryType) => new()
    {
        LibraryType = libraryType,
        IncludePrefix = false
    };
}
