using System.Collections.Generic;

namespace API.DTOs.CollectionTags
{
    public class CollectionTagBulkAddDto
    {
        /// <summary>
        /// Collection Tag Id
        /// </summary>
        public int CollectionTagId { get; init; }
        /// <summary>
        /// Series Ids to add onto Collection Tag
        /// </summary>
        public IEnumerable<int> SeriesIds { get; init; }
    }
}
