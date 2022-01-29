using System;
using API.Entities.Enums;

namespace API.DTOs;

/// <summary>
/// A mesh of data for Recently added volume/chapters
/// </summary>
public class RecentlyAddedItemDto
{
    public string SeriesName { get; set; }
    public int SeriesId { get; set; }
    public int LibraryId { get; set; }
    public LibraryType LibraryType { get; set; }
    /// <summary>
    /// This will automatically map to Volume X, Chapter Y, etc.
    /// </summary>
    public string Title { get; set; }
    public DateTime Created { get; set; }

}
