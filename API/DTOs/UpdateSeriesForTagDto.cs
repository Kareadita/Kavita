using System.Collections.Generic;

namespace API.DTOs
{
    public class UpdateSeriesForTagDto
    {
        public CollectionTagDto Tag { get; init; }
        public ICollection<int> SeriesIdsToRemove { get; init; }
    }
}