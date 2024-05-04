using System;

namespace API.DTOs.CollectionTags;

[Obsolete("Use AppUserCollectionDto")]
public class CollectionTagDto
{
    public int Id { get; set; }
    public string Title { get; set; } = default!;
    public string Summary { get; set; } = default!;
    public bool Promoted { get; set; }
    /// <summary>
    /// The cover image string. This is used on Frontend to show or hide the Cover Image
    /// </summary>
    public string CoverImage { get; set; } = default!;
    public bool CoverImageLocked { get; set; }
}
