using System.Collections.Generic;

namespace API.DTOs.WantToRead;

public class UpdateWantToReadDto
{
    /// <summary>
    /// List of Series Ids that will be Added/Removed
    /// </summary>
    public IList<int> SeriesIds { get; set; }
}
