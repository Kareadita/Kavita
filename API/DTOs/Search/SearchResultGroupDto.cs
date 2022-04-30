using System.Collections.Generic;
using API.DTOs.CollectionTags;
using API.DTOs.Metadata;
using API.DTOs.ReadingLists;

namespace API.DTOs.Search;

/// <summary>
/// Represents all Search results for a query
/// </summary>
public class SearchResultGroupDto
{
    public IEnumerable<LibraryDto> Libraries { get; set; }
    public IEnumerable<SearchResultDto> Series { get; set; }
    public IEnumerable<CollectionTagDto> Collections { get; set; }
    public IEnumerable<ReadingListDto> ReadingLists { get; set; }
    public IEnumerable<PersonDto> Persons { get; set; }
    public IEnumerable<GenreTagDto> Genres { get; set; }
    public IEnumerable<TagDto> Tags { get; set; }
    public IEnumerable<MangaFileDto> Files { get; set; }


}
