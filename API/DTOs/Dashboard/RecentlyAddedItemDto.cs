using System;
using API.Entities.Enums;

namespace API.DTOs.Dashboard;

/// <summary>
/// A mesh of data for Recently added volume/chapters
/// </summary>
public class RecentlyAddedItemDto
{
    public string SeriesName { get; set; } = default!;
    public int SeriesId { get; set; }
    public int LibraryId { get; set; }
    public LibraryType LibraryType { get; set; }
    /// <summary>
    /// This will automatically map to Volume X, Chapter Y, etc.
    /// </summary>
    public string Title { get; set; } = default!;
    public DateTime Created { get; set; }
    /// <summary>
    /// Chapter Id if this is a chapter. Not guaranteed to be set.
    /// </summary>
    public int ChapterId { get; set; } = 0;
    /// <summary>
    /// Volume Id if this is a chapter. Not guaranteed to be set.
    /// </summary>
    public int VolumeId { get; set; } = 0;
    /// <summary>
    /// This is used only on the UI. It is just index of being added.
    /// </summary>
    public int Id { get; set; }
    public MangaFormat Format { get; set; }

}
