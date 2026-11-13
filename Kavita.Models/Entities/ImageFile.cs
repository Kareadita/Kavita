using System;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Interfaces;
using Kavita.Models.Images;

namespace Kavita.Models.Entities;

#nullable enable

/// <summary>
/// An image file Kavita manages on disk. All images use this system. One row can be shared, such as a volume
/// and its series showing a chapter's cover
/// </summary>
public class ImageFile : IEntityDate
{
    public int Id { get; set; }
    public ImageKind Kind { get; set; }
    /// <summary>
    /// Path relative to the folder <see cref="Kind"/> is stored in
    /// </summary>
    public required string FileName { get; set; }
    public ImageOrigin Origin { get; set; }

    public EncodeFormat? Format { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    /// <summary>
    /// Size of the image this file was made from, before it was resized
    /// </summary>
    public int? SourceWidth { get; set; }
    public int? SourceHeight { get; set; }

    /// <summary>
    /// What the image belongs to when no entity points at it: the host for a favicon, the name for a publisher
    /// </summary>
    public string? Key { get; set; }

    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }

    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastModifiedUtc { get; set; }
}
