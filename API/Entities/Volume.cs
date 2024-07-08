﻿using System;
using System.Collections.Generic;
using API.Entities.Interfaces;
using API.Extensions;
using API.Services.Tasks.Scanner.Parser;

namespace API.Entities;

public class Volume : IEntityDate, IHasReadTimeEstimate
{
    public int Id { get; set; }
    /// <summary>
    /// A String representation of the volume number. Allows for floats. Can also include a range (1-2).
    /// </summary>
    /// <remarks>For Books with Series_index, this will map to the Series Index.</remarks>
    public required string Name { get; set; }
    /// <summary>
    /// This is just the original Parsed volume number for lookups
    /// </summary>
    public string LookupName { get; set; }
    /// <summary>
    /// The minimum number in the Name field in Int form
    /// </summary>
    /// <remarks>Removed in v0.7.13.8, this was an int and we need the ability to have 0.5 volumes render on the UI</remarks>
    [Obsolete("Use MinNumber and MaxNumber instead")]
    public int Number { get; set; }
    /// <summary>
    /// The minimum number in the Name field
    /// </summary>
    public required float MinNumber { get; set; }
    /// <summary>
    /// The maximum number in the Name field (same as Minimum if Name isn't a range)
    /// </summary>
    public required float MaxNumber { get; set; }
    public IList<Chapter> Chapters { get; set; } = null!;
    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastModifiedUtc { get; set; }

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

    /// <summary>
    /// Returns the Chapter Number. If the chapter is a range, returns that, formatted.
    /// </summary>
    /// <returns></returns>
    public string GetNumberTitle()
    {
        if (MinNumber.Is(MaxNumber))
        {
            return $"{MinNumber}";
        }
        return $"{MinNumber}-{MaxNumber}";
    }

}
