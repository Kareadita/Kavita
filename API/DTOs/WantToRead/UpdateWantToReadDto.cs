using System;
using System.Collections.Generic;

namespace API.DTOs.WantToRead;

/// <summary>
/// A list of Series to pass when working with Want To Read APIs
/// </summary>
public class UpdateWantToReadDto
{
    /// <summary>
    /// List of Series Ids that will be Added/Removed
    /// </summary>
    public IList<int> SeriesIds { get; set; } = ArraySegment<int>.Empty;
}
