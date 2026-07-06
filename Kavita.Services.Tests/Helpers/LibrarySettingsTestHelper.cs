using System.Linq;
using Kavita.Models.DTOs;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;

namespace Kavita.Services.Tests.Helpers;

/// <summary>
/// Mirrors <see cref="Kavita.Server.Controllers.LibraryController.UpdateLibrarySettings"/> for integration tests.
/// </summary>
internal static class LibrarySettingsTestHelper
{
    public static UpdateLibraryDto ToUpdateDto(Library library, bool enablePdfExternalLinks, bool enablePdfInternalLinks)
    {
        return new UpdateLibraryDto
        {
            Id = library.Id,
            Name = library.Name,
            Type = library.Type,
            Folders = library.Folders.Select(f => f.Path),
            FolderWatching = library.FolderWatching,
            IncludeInDashboard = library.IncludeInDashboard,
            IncludeInSearch = library.IncludeInSearch,
            ManageCollections = library.ManageCollections,
            ManageReadingLists = library.ManageReadingLists,
            AllowScrobbling = library.AllowScrobbling,
            AllowMetadataMatching = library.AllowMetadataMatching,
            EnableMetadata = library.EnableMetadata,
            RemovePrefixForSortName = library.RemovePrefixForSortName,
            InheritWebLinksFromFirstChapter = library.InheritWebLinksFromFirstChapter,
            EnablePdfExternalLinks = enablePdfExternalLinks,
            EnablePdfInternalLinks = enablePdfInternalLinks,
            DefaultLanguage = library.DefaultLanguage,
            MetadataProvider = library.MetadataProvider,
            FileGroupTypes = library.LibraryFileTypes.Select(t => t.FileTypeGroup).ToList(),
            ExcludePatterns = library.LibraryExcludePatterns.Select(p => p.Pattern).ToList(),
        };
    }

    public static void ApplyUpdateLibrarySettings(UpdateLibraryDto dto, Library library, bool updateType = true)
    {
        if (updateType)
        {
            library.Type = dto.Type;
        }

        library.FolderWatching = dto.FolderWatching;
        library.IncludeInDashboard = dto.IncludeInDashboard;
        library.IncludeInSearch = dto.IncludeInSearch;
        library.ManageCollections = dto.ManageCollections;
        library.ManageReadingLists = dto.ManageReadingLists;
        library.AllowScrobbling = dto.AllowScrobbling;
        library.AllowMetadataMatching = dto.AllowMetadataMatching;
        library.EnableMetadata = dto.EnableMetadata;
        library.RemovePrefixForSortName = dto.RemovePrefixForSortName;
        library.InheritWebLinksFromFirstChapter = dto.InheritWebLinksFromFirstChapter;
        library.EnablePdfExternalLinks = dto.EnablePdfExternalLinks;
        library.EnablePdfInternalLinks = dto.EnablePdfInternalLinks;
        library.DefaultLanguage = dto.DefaultLanguage;
        library.MetadataProvider = dto.MetadataProvider;

        library.LibraryFileTypes = dto.FileGroupTypes
            .Select(t => new LibraryFileTypeGroup { FileTypeGroup = t, LibraryId = library.Id })
            .Distinct()
            .ToList();

        library.LibraryExcludePatterns = dto.ExcludePatterns
            .Distinct()
            .Select(t => new LibraryExcludePattern { Pattern = t, LibraryId = library.Id })
            .ToList();
    }

    public static void CopySettingsFromLibrary(Library source, Library target, bool includeType = false)
    {
        ApplyUpdateLibrarySettings(new UpdateLibraryDto
        {
            Folders = target.Folders.Select(s => s.Path),
            Name = target.Name,
            Id = target.Id,
            Type = source.Type,
            AllowScrobbling = source.AllowScrobbling,
            AllowMetadataMatching = source.AllowMetadataMatching,
            EnableMetadata = source.EnableMetadata,
            RemovePrefixForSortName = source.RemovePrefixForSortName,
            InheritWebLinksFromFirstChapter = source.InheritWebLinksFromFirstChapter,
            EnablePdfExternalLinks = source.EnablePdfExternalLinks,
            EnablePdfInternalLinks = source.EnablePdfInternalLinks,
            DefaultLanguage = source.DefaultLanguage,
            MetadataProvider = source.MetadataProvider,
            ExcludePatterns = source.LibraryExcludePatterns.Select(p => p.Pattern).ToList(),
            FolderWatching = source.FolderWatching,
            ManageCollections = source.ManageCollections,
            FileGroupTypes = source.LibraryFileTypes.Select(t => t.FileTypeGroup).ToList(),
            IncludeInDashboard = source.IncludeInDashboard,
            IncludeInSearch = source.IncludeInSearch,
            ManageReadingLists = source.ManageReadingLists,
        }, target, includeType);
    }
}
