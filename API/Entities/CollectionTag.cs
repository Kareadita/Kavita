using System.Collections.Generic;
using API.Entities.Metadata;
using Microsoft.EntityFrameworkCore;

namespace API.Entities;

/// <summary>
/// Represents a user entered field that is used as a tagging and grouping mechanism
/// </summary>
[Index(nameof(Id), nameof(Promoted), IsUnique = true)]
public class CollectionTag
{
    public int Id { get; set; }
    /// <summary>
    /// Visible title of the Tag
    /// </summary>
    public string Title { get; set; }
    /// <summary>
    /// Absolute path to the (managed) image file
    /// </summary>
    /// <remarks>The file is managed internally to Kavita's APPDIR</remarks>
    public string CoverImage { get; set; }
    /// <summary>
    /// Denotes if the CoverImage has been overridden by the user. If so, it will not be updated during normal scan operations.
    /// </summary>
    public bool CoverImageLocked { get; set; }

    /// <summary>
    /// A description of the tag
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// A normalized string used to check if the tag already exists in the DB
    /// </summary>
    public string NormalizedTitle { get; set; }
    /// <summary>
    /// A promoted collection tag will allow all linked seriesMetadata's Series to show for all users.
    /// </summary>
    public bool Promoted { get; set; }

    public ICollection<SeriesMetadata> SeriesMetadatas { get; set; }

    /// <summary>
    /// Not Used due to not using concurrency update
    /// </summary>
    public uint RowVersion { get; private set; }

    /// <summary>
    /// Not Used due to not using concurrency update
    /// </summary>
    public void OnSavingChanges()
    {
        RowVersion++;
    }
}
