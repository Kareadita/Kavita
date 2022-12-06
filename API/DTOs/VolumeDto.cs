
using System;
using System.Collections.Generic;
using API.Entities;
using API.Entities.Interfaces;

namespace API.DTOs;

public class VolumeDto : IHasReadTimeEstimate
{
    public int Id { get; set; }
    /// <inheritdoc cref="Volume.Number"/>
    public int Number { get; set; }

    /// <inheritdoc cref="Volume.Name"/>
    public string Name { get; set; } = default!;
    public int Pages { get; set; }
    public int PagesRead { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime Created { get; set; }
    public int SeriesId { get; set; }
    public ICollection<ChapterDto> Chapters { get; set; } = new List<ChapterDto>();
    /// <inheritdoc cref="IHasReadTimeEstimate.MinHoursToRead"/>
    public int MinHoursToRead { get; set; }
    /// <inheritdoc cref="IHasReadTimeEstimate.MaxHoursToRead"/>
    public int MaxHoursToRead { get; set; }
    /// <inheritdoc cref="IHasReadTimeEstimate.AvgHoursToRead"/>
    public int AvgHoursToRead { get; set; }
}
