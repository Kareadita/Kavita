using System;

namespace Kavita.Models.Entities.Interfaces;

#nullable enable

// TODO: Refactor after pipeline work to make this for the DTOs public bool HasCoverImage { get; set; } which AutoMapper can set based on ImageFileId != null
[Obsolete("Use IHasCoverImageFile")]
public interface IHasCoverImage
{
    /// <summary>
    /// Absolute path to the (managed) image file
    /// </summary>
    /// <remarks>The file is managed internally to Kavita's APPDIR</remarks>
    public string? CoverImage { get; set; }

    /// <summary>
    /// Primary color derived from the Cover Image
    /// </summary>
    public string? PrimaryColor { get; set; }
    /// <summary>
    /// Secondary color derived from the Cover Image
    /// </summary>
    public string? SecondaryColor { get; set; }

    /// <summary>
    /// Nulls out the ColorScape properties
    /// </summary>
    void ResetColorScape();
}
