using System;

namespace API.DTOs.CollectionTags;

[Obsolete("Use AppUserCollectionDto")]
public sealed record CollectionTagDto
{
    /// <inheritdoc cref="API.Entities.CollectionTag.Id"/>
    public int Id { get; set; }
    /// <inheritdoc cref="API.Entities.CollectionTag.Title"/>
    public string Title { get; set; } = default!;
    /// <inheritdoc cref="API.Entities.CollectionTag.Summary"/>
    public string Summary { get; set; } = default!;
    /// <inheritdoc cref="API.Entities.CollectionTag.Promoted"/>
    public bool Promoted { get; set; }
    /// <summary>
    /// The cover image string. This is used on Frontend to show or hide the Cover Image
    /// </summary>
    /// <inheritdoc cref="API.Entities.CollectionTag.CoverImage"/>
    public string CoverImage { get; set; } = default!;
    /// <inheritdoc cref="API.Entities.CollectionTag.CoverImageLocked"/>
    public bool CoverImageLocked { get; set; }
}
