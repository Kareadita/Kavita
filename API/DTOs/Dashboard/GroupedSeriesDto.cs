using System;
using API.Entities.Enums;

namespace API.DTOs.Dashboard;
/// <summary>
/// This is a representation of a Series with some amount of underlying files within it. This is used for Recently Updated Series section
/// </summary>
public class GroupedSeriesDto
{
    public string SeriesName { get; set; } = default!;
    public int SeriesId { get; set; }
    public int LibraryId { get; set; }
    public LibraryType LibraryType { get; set; }
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
    /// <summary>
    /// Number of items that are updated. This provides a sort of grouping when multiple chapters are added per Volume/Series
    /// </summary>
    public int Count { get; set; }
}
