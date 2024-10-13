using System.Collections.Generic;
using API.DTOs.Collection;
using API.DTOs.CollectionTags;
using API.DTOs.Metadata;
using API.DTOs.Reader;
using API.DTOs.ReadingLists;

namespace API.DTOs.Search;

/// <summary>
/// Represents all Search results for a query
/// </summary>
public class SearchResultGroupDto
{
    public IEnumerable<LibraryDto> Libraries { get; set; } = default!;
    public IEnumerable<SearchResultDto> Series { get; set; } = default!;
    public IEnumerable<AppUserCollectionDto> Collections { get; set; } = default!;
    public IEnumerable<ReadingListDto> ReadingLists { get; set; } = default!;
    public IEnumerable<PersonDto> Persons { get; set; } = default!;
    public IEnumerable<GenreTagDto> Genres { get; set; } = default!;
    public IEnumerable<TagDto> Tags { get; set; } = default!;
    public IEnumerable<MangaFileDto> Files { get; set; } = default!;
    public IEnumerable<ChapterDto> Chapters { get; set; } = default!;
    public IEnumerable<BookmarkSearchResultDto> Bookmarks { get; set; } = default!;


}
