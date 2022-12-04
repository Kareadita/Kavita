using System;
using System.Collections.Generic;
using API.Entities.Interfaces;

namespace API.Entities;

public class Volume : IEntityDate, IHasReadTimeEstimate
{
    public int Id { get; set; }
    /// <summary>
    /// A String representation of the volume number. Allows for floats.
    /// </summary>
    /// <remarks>For Books with Series_index, this will map to the Series Index.</remarks>
    public required string Name { get; set; }
    /// <summary>
    /// The minimum number in the Name field in Int form
    /// </summary>
    public int Number { get; set; }
    public IList<Chapter> Chapters { get; set; } = null!;
    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
    /// <summary>
    /// Absolute path to the (managed) image file
    /// </summary>
    /// <remarks>The file is managed internally to Kavita's APPDIR</remarks>
    public string? CoverImage { get; set; }
    /// <summary>
    /// Total pages of all chapters in this volume
    /// </summary>
    public int Pages { get; set; }
    /// <summary>
    /// Total Word count of all chapters in this volume.
    /// </summary>
    /// <remarks>Word Count is only available from EPUB files</remarks>
    public long WordCount { get; set; }
    public int MinHoursToRead { get; set; }
    public int MaxHoursToRead { get; set; }
    public int AvgHoursToRead { get; set; }


    // Relationships
    public Series Series { get; set; } = null!;
    public int SeriesId { get; set; }

}
