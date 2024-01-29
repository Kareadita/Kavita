
using System;
using System.Collections.Generic;
using API.Entities;
using API.Entities.Interfaces;

namespace API.DTOs;

public class VolumeDto : IHasReadTimeEstimate
{
    public int Id { get; set; }
    /// <inheritdoc cref="Volume.MinNumber"/>
    public float MinNumber { get; set; }
    /// <inheritdoc cref="Volume.MaxNumber"/>
    public float MaxNumber { get; set; }
    /// <inheritdoc cref="Volume.Name"/>
    public string Name { get; set; } = default!;
    /// <summary>
    /// This will map to MinNumber. Number was removed in v0.7.13.8
    /// </summary>
    [Obsolete("Use MinNumber")]
    public float Number { get; set; }
    public int Pages { get; set; }
    public int PagesRead { get; set; }
    public DateTime LastModifiedUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
    /// <summary>
    /// When chapter was created in local server time
    /// </summary>
    /// <remarks>This is required for Tachiyomi Extension</remarks>
    public DateTime Created { get; set; }
    /// <summary>
    /// When chapter was last modified in local server time
    /// </summary>
    /// <remarks>This is required for Tachiyomi Extension</remarks>
    public DateTime LastModified { get; set; }
    public int SeriesId { get; set; }
    public ICollection<ChapterDto> Chapters { get; set; } = new List<ChapterDto>();
    /// <inheritdoc cref="IHasReadTimeEstimate.MinHoursToRead"/>
    public int MinHoursToRead { get; set; }
    /// <inheritdoc cref="IHasReadTimeEstimate.MaxHoursToRead"/>
    public int MaxHoursToRead { get; set; }
    /// <inheritdoc cref="IHasReadTimeEstimate.AvgHoursToRead"/>
    public int AvgHoursToRead { get; set; }
}
