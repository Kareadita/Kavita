using System;
using System.Collections.Generic;
using API.Entities.Interfaces;
using API.Extensions;
using API.Services.Tasks.Scanner.Parser;

namespace API.DTOs;

public sealed record VolumeDto : IHasReadTimeEstimate, IHasCoverImage
{
    /// <inheritdoc cref="API.Entities.Volume.Id"/>
    public int Id { get; set; }
    /// <inheritdoc cref="API.Entities.Volume.MinNumber"/>
    public float MinNumber { get; set; }
    /// <inheritdoc cref="API.Entities.Volume.MaxNumber"/>
    public float MaxNumber { get; set; }
    /// <inheritdoc cref="API.Entities.Volume.Name"/>
    public string Name { get; set; } = default!;
    /// <summary>
    /// This will map to MinNumber. Number was removed in v0.7.13.8/v0.7.14
    /// </summary>
    [Obsolete("Use MinNumber")]
    public int Number { get; set; }
    public int Pages { get; set; }
    public int PagesRead { get; set; }
    /// <inheritdoc cref="API.Entities.Volume.LastModifiedUtc"/>
    public DateTime LastModifiedUtc { get; set; }
    /// <inheritdoc cref="API.Entities.Volume.CreatedUtc"/>
    public DateTime CreatedUtc { get; set; }
    /// <summary>
    /// When chapter was created in local server time
    /// </summary>
    /// <remarks>This is required for Tachiyomi Extension</remarks>
    /// <inheritdoc cref="API.Entities.Volume.Created"/>
    public DateTime Created { get; set; }
    /// <summary>
    /// When chapter was last modified in local server time
    /// </summary>
    /// <remarks>This is required for Tachiyomi Extension</remarks>
    /// <inheritdoc cref="API.Entities.Volume.LastModified"/>
    public DateTime LastModified { get; set; }
    public int SeriesId { get; set; }
    public ICollection<ChapterDto> Chapters { get; set; } = new List<ChapterDto>();
    /// <inheritdoc cref="IHasReadTimeEstimate.MinHoursToRead"/>
    public int MinHoursToRead { get; set; }
    /// <inheritdoc cref="IHasReadTimeEstimate.MaxHoursToRead"/>
    public int MaxHoursToRead { get; set; }
    /// <inheritdoc cref="IHasReadTimeEstimate.AvgHoursToRead"/>
    public float AvgHoursToRead { get; set; }
    public long WordCount { get; set; }

    /// <summary>
    /// Is this a loose leaf volume
    /// </summary>
    /// <returns></returns>
    public bool IsLooseLeaf()
    {
        return MinNumber.Is(Parser.LooseLeafVolumeNumber);
    }

    /// <summary>
    /// Does this volume hold only specials
    /// </summary>
    /// <returns></returns>
    public bool IsSpecial()
    {
        return MinNumber.Is(Parser.SpecialVolumeNumber);
    }

    /// <inheritdoc cref="API.Entities.Volume.CoverImage"/>
    public string CoverImage { get; set; }
    /// <inheritdoc cref="API.Entities.Volume.CoverImageLocked"/>
    private bool CoverImageLocked { get; set; }
    /// <inheritdoc cref="API.Entities.Volume.PrimaryColor"/>
    public string? PrimaryColor { get; set; } = string.Empty;
    /// <inheritdoc cref="API.Entities.Volume.SecondaryColor"/>
    public string? SecondaryColor { get; set; } = string.Empty;

    public void ResetColorScape()
    {
        PrimaryColor = string.Empty;
        SecondaryColor = string.Empty;
    }
}
