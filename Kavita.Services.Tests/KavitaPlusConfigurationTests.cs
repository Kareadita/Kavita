using Kavita.API.Services.Plus;
using Kavita.Models.Entities.Enums;

namespace Kavita.Services.Tests;

/// <summary>
/// Covers <see cref="KavitaPlusConfiguration.IsValidMetadataProviderForLibraryType"/>, which is shared between
/// validating a Library's default Metadata Provider and a Series-level Metadata Provider override.
/// </summary>
public class KavitaPlusConfigurationTests
{
    [Theory]
    [InlineData(LibraryType.Manga, MetadataProvider.Mangabaka, true)]
    [InlineData(LibraryType.Manga, MetadataProvider.Hardcover, false)]
    [InlineData(LibraryType.Manga, MetadataProvider.ComicBookRoundup, false)]

    [InlineData(LibraryType.Comic, MetadataProvider.ComicBookRoundup, true)]
    [InlineData(LibraryType.Comic, MetadataProvider.Hardcover, true)]
    [InlineData(LibraryType.Comic, MetadataProvider.Mangabaka, false)]

    [InlineData(LibraryType.Book, MetadataProvider.Hardcover, true)]
    [InlineData(LibraryType.Book, MetadataProvider.Mangabaka, true)]
    [InlineData(LibraryType.Book, MetadataProvider.ComicBookRoundup, false)]

    [InlineData(LibraryType.Image, MetadataProvider.Mangabaka, true)]
    [InlineData(LibraryType.Image, MetadataProvider.ComicBookRoundup, true)]
    [InlineData(LibraryType.Image, MetadataProvider.Hardcover, false)]

    [InlineData(LibraryType.LightNovel, MetadataProvider.Mangabaka, true)]
    [InlineData(LibraryType.LightNovel, MetadataProvider.Hardcover, true)]
    [InlineData(LibraryType.LightNovel, MetadataProvider.ComicBookRoundup, false)]

    [InlineData(LibraryType.ComicVine, MetadataProvider.ComicBookRoundup, true)]
    [InlineData(LibraryType.ComicVine, MetadataProvider.Hardcover, true)]
    [InlineData(LibraryType.ComicVine, MetadataProvider.Mangabaka, false)]
    public void IsValidMetadataProviderForLibraryType_MatchesConfiguredSet(LibraryType libraryType, MetadataProvider provider, bool expected)
    {
        Assert.Equal(expected, KavitaPlusConfiguration.IsValidMetadataProviderForLibraryType(libraryType, provider));
    }
}
