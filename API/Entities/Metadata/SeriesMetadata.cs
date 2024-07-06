using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using API.Entities.Enums;
using API.Entities.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace API.Entities.Metadata;

[Index(nameof(Id), nameof(SeriesId), IsUnique = true)]
public class SeriesMetadata : IHasConcurrencyToken
{
    public int Id { get; set; }

    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Highest Age Rating from all Chapters
    /// </summary>
    public AgeRating AgeRating { get; set; }
    /// <summary>
    /// Earliest Year from all chapters
    /// </summary>
    public int ReleaseYear { get; set; }
    /// <summary>
    /// Language of the content (BCP-47 code)
    /// </summary>
    public string Language { get; set; } = string.Empty;
    /// <summary>
    /// Total expected number of issues/volumes in the series from ComicInfo.xml
    /// </summary>
    public int TotalCount { get; set; } = 0;
    /// <summary>
    /// Max number of issues/volumes in the series (Max of Volume/Number field in ComicInfo)
    /// </summary>
    public int MaxCount { get; set; } = 0;
    public PublicationStatus PublicationStatus { get; set; }
    /// <summary>
    /// A Comma-separated list of strings representing links from the series
    /// </summary>
    /// <remarks>This is not populated from Chapters of the Series</remarks>
    public string WebLinks { get; set; } = string.Empty;

    #region Locks

    public bool LanguageLocked { get; set; }
    public bool SummaryLocked { get; set; }
    /// <summary>
    /// Locked by user so metadata updates from scan loop will not override AgeRating
    /// </summary>
    public bool AgeRatingLocked { get; set; }
    /// <summary>
    /// Locked by user so metadata updates from scan loop will not override PublicationStatus
    /// </summary>
    public bool PublicationStatusLocked { get; set; }
    public bool GenresLocked { get; set; }
    public bool TagsLocked { get; set; }
    public bool WriterLocked { get; set; }
    public bool CharacterLocked { get; set; }
    public bool ColoristLocked { get; set; }
    public bool EditorLocked { get; set; }
    public bool InkerLocked { get; set; }
    public bool ImprintLocked { get; set; }
    public bool LettererLocked { get; set; }
    public bool PencillerLocked { get; set; }
    public bool PublisherLocked { get; set; }
    public bool TranslatorLocked { get; set; }
    public bool TeamLocked { get; set; }
    public bool LocationLocked { get; set; }
    public bool CoverArtistLocked { get; set; }
    public bool ReleaseYearLocked { get; set; }

    #endregion

    #region Relationships

    [Obsolete("Use AppUserCollection instead")]
    public ICollection<CollectionTag> CollectionTags { get; set; } = new List<CollectionTag>();

    public ICollection<Genre> Genres { get; set; } = new List<Genre>();
    public ICollection<Tag> Tags { get; set; } = new List<Tag>();

    /// <summary>
    /// All people attached at a Series level.
    /// </summary>
    public ICollection<SeriesMetadataPeople> People { get; set; } = new List<SeriesMetadataPeople>();

    public int SeriesId { get; set; }
    public Series Series { get; set; } = null!;

    #endregion


    /// <inheritdoc />
    [ConcurrencyCheck]
    public uint RowVersion { get; private set; }


    /// <inheritdoc />
    public void OnSavingChanges()
    {
        RowVersion++;
    }
}
