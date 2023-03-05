using System.Collections.Generic;

namespace API.DTOs.CollectionTags;

public class CollectionTagBulkAddDto
{
    /// <summary>
    /// Collection Tag Id
    /// </summary>
    /// <remarks>Can be 0 which then will use Title to create a tag</remarks>
    public int CollectionTagId { get; init; }
    public string CollectionTagTitle { get; init; } = default!;
    /// <summary>
    /// Series Ids to add onto Collection Tag
    /// </summary>
    public IEnumerable<int> SeriesIds { get; init; } = default!;
}
