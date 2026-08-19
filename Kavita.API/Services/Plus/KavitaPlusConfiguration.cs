using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Kavita.Models.Entities.Enums;

namespace Kavita.API.Services.Plus;

/// <summary>
/// This class should hold readonly & static configuration for Kavita Plus. What can we used where etc.
/// </summary>
public static class KavitaPlusConfiguration
{

    /// <remarks>When adjusting these, also adjust in LibrarySettingsModalComponent in the UI</remarks>
    public static readonly IReadOnlyDictionary<LibraryType, IReadOnlySet<MetadataProvider>> MetadataProvidersForLibraryTypes =
        new ReadOnlyDictionary<LibraryType, IReadOnlySet<MetadataProvider>>(
            new Dictionary<LibraryType, IReadOnlySet<MetadataProvider>>
            {
                [LibraryType.Manga] = new HashSet<MetadataProvider>
                {
                    MetadataProvider.Mangabaka
                },
                [LibraryType.Comic] = new HashSet<MetadataProvider>
                {
                    MetadataProvider.ComicBookRoundup,
                    MetadataProvider.Hardcover
                },
                [LibraryType.Book] = new HashSet<MetadataProvider>
                {
                    MetadataProvider.Hardcover,
                    MetadataProvider.Mangabaka
                },
                [LibraryType.Image] = new HashSet<MetadataProvider>
                {
                    MetadataProvider.Mangabaka,
                    MetadataProvider.ComicBookRoundup
                },
                [LibraryType.LightNovel] = new HashSet<MetadataProvider>
                {
                    MetadataProvider.Mangabaka,
                    MetadataProvider.Hardcover
                },
                [LibraryType.ComicVine] = new HashSet<MetadataProvider>
                {
                    MetadataProvider.ComicBookRoundup,
                    MetadataProvider.Hardcover
                }
            });

    /// <remarks>When adjusting these, also adjust in ManageScrobbleProvidersComponent in the UI</remarks>>
    public static readonly IReadOnlyDictionary<LibraryType, IReadOnlySet<ScrobbleProvider>> ScrobbleProvidersForLibraryTypes =
        new ReadOnlyDictionary<LibraryType, IReadOnlySet<ScrobbleProvider>>(
            new Dictionary<LibraryType, IReadOnlySet<ScrobbleProvider>>
            {
                [LibraryType.Book] = new HashSet<ScrobbleProvider>
                {
                    ScrobbleProvider.Hardcover
                },
                [LibraryType.LightNovel] = new HashSet<ScrobbleProvider>
                {
                    ScrobbleProvider.AniList,
                    ScrobbleProvider.Hardcover,
                    ScrobbleProvider.MangaBaka,
                    ScrobbleProvider.Mal
                },
                [LibraryType.Comic] = new HashSet<ScrobbleProvider>
                {
                    ScrobbleProvider.Hardcover
                },
                [LibraryType.ComicVine] = new HashSet<ScrobbleProvider>
                {
                    ScrobbleProvider.Hardcover
                },
                [LibraryType.Manga] = new HashSet<ScrobbleProvider>
                {
                    ScrobbleProvider.AniList,
                    ScrobbleProvider.MangaBaka,
                    ScrobbleProvider.Mal
                }
            });

    /// <summary>
    /// All currently in use scrobble providers. Generated from <see cref="ScrobbleProvidersForLibraryTypes"/>
    /// </summary>
    public static readonly IReadOnlyList<ScrobbleProvider> AllInUseScrobbleProviders = ScrobbleProvidersForLibraryTypes
        .Values.SelectMany(s => s).Distinct().ToList().AsReadOnly();

    /// <summary>
    /// Is the given <see cref="MetadataProvider"/> valid (supported) for the given <see cref="LibraryType"/>.
    /// Used to validate both a Library's default provider and a Series-level provider override.
    /// </summary>
    public static bool IsValidMetadataProviderForLibraryType(LibraryType type, MetadataProvider provider)
    {
        return MetadataProvidersForLibraryTypes.TryGetValue(type, out var validProviders) && validProviders.Contains(provider);
    }

}
