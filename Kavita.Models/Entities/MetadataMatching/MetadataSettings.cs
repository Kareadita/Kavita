using System.Collections.Generic;
using Kavita.Models.DTOs.KavitaPlus.Metadata;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.KavitaPlus;

namespace Kavita.Models.Entities.MetadataMatching;

/// <summary>
/// Handles the metadata settings for Kavita+
/// </summary>
public class MetadataSettings
{
    public int Id { get; set; }
    /// <summary>
    /// If writing any sort of metadata from upstream (AniList, Hardcover) source is allowed
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Enable processing of metadata outside K+; e.g. disk and API
    /// </summary>
    public bool EnableExtendedMetadataProcessing { get; set; }

    #region Series Metadata

    /// <summary>
    /// Allow the Summary to be written
    /// </summary>
    public bool EnableSummary { get; set; }
    /// <summary>
    /// Allow Publication status to be derived and updated
    /// </summary>
    public bool EnablePublicationStatus { get; set; }
    /// <summary>
    /// Allow Age Rating status to be derived and updated
    /// </summary>
    public bool EnableAgeRating { get; set; }
    /// <summary>
    /// Allow Relationships between series to be set
    /// </summary>
    public bool EnableRelationships { get; set; }
    /// <summary>
    /// Allow People to be created (including downloading images)
    /// </summary>
    public bool EnablePeople { get; set; }
    /// <summary>
    /// Allow Start date to be set within the Series
    /// </summary>
    public bool EnableStartDate { get; set; }
    /// <summary>
    /// Allow setting the Localized name
    /// </summary>
    public bool EnableLocalizedName { get; set; }
    /// <summary>
    /// Allow setting the Series name
    /// </summary>
    public bool EnableName { get; set; }
    /// <summary>
    /// Allow setting the cover image
    /// </summary>
    public bool EnableCoverImage { get; set; }
    #endregion

    #region Chapter Metadata
    /// <summary>
    /// Allow Summary to be set within Chapter/Issue
    /// </summary>
    public bool EnableChapterSummary { get; set; }
    /// <summary>
    /// Allow Release Date to be set within Chapter/Issue
    /// </summary>
    public bool EnableChapterReleaseDate { get; set; }
    /// <summary>
    /// Allow Title to be set within Chapter/Issue
    /// </summary>
    public bool EnableChapterTitle { get; set; }
    /// <summary>
    /// Allow Publisher to be set within Chapter/Issue
    /// </summary>
    public bool EnableChapterPublisher { get; set; }
    /// <summary>
    /// Allow setting the cover image for the Chapter/Issue
    /// </summary>
    public bool EnableChapterCoverImage { get; set; }
    #endregion

    #region Volume Metadata
    /// <summary>
    /// Allow setting the cover image for the Volume
    /// </summary>
    public bool EnableVolumeCoverImage { get; set; }
    #endregion

    // Need to handle the Genre/tags stuff
    public bool EnableGenres { get; set; } = true;
    public bool EnableTags { get; set; } = true;

    /// <summary>
    /// For Authors and Writers, how should names be stored (Exclusively applied for AniList). This does not affect Character names.
    /// </summary>
    public bool FirstLastPeopleNaming { get; set; }

    /// <summary>
    /// Server-wide semicolon separated list of BCP-47 language codes to prioritize for <c>Series.Name</c>
    /// </summary>
    public string GlobalNameLanguages { get; set; } = "en";

    /// <summary>
    /// Server-wide semicolon separated list of BCP-47 language codes to prioritize for <c>Series.LocalizedName</c>
    /// </summary>
    public string GlobalLocalizedNameLanguages { get; set; } = "ja-Latn";

    /// <summary>
    /// Per-library overrides of the global language priority, keyed by LibraryId.
    /// </summary>
    /// <remarks>An override fully replaces the global list for that library, it does not prepend to it.</remarks>
    public Dictionary<int, SeriesNameLanguage> LibraryLanguageTitleOverrides { get; set; } = [];

    /// <summary>
    /// Any Genres or Tags that if present, will trigger an Age Rating Override. Highest rating will be prioritized for matching.
    /// </summary>
    public Dictionary<string, AgeRating> AgeRatingMappings { get; set; }

    /// <summary>
    /// A list of rules that allow mapping a genre/tag to another genre/tag
    /// </summary>
    public List<MetadataFieldMapping> FieldMappings { get; set; }

    /// <summary>
    /// A list of overrides that will enable writing to locked fields
    /// </summary>
    public List<MetadataSettingField> Overrides { get; set; }

    /// <summary>
    /// Do not allow any Genre/Tag in this list to be written to Kavita
    /// </summary>
    public List<string> Blacklist { get; set; }

    /// <summary>
    /// Only allow these Tags to be written to Kavita
    /// </summary>
    public List<string> Whitelist { get; set; }

    /// <summary>
    /// Which Roles to allow metadata downloading for
    /// </summary>
    public List<PersonRole> PersonRoles { get; set; }
    /// <summary>
    /// Optional mappings of strings -> ComicInfo Age Ratings
    /// </summary>
    public Dictionary<string, AgeRating> ExternalAgeRatingMappings { get; set; } = [];
    /// <summary>
    /// Filter Tags above this level. This takes effect after tag mapping.
    /// </summary>
    /// <example>FilterAboveWeight = Recurrent, all tags with weight above Recurrent (incidental, unweighted) will be filtered out after tag mapping step</example>
    /// <remarks>This is only applicable for <see cref="MetadataProvider.Mangabaka"/>. Tag weights are: Core, Defining, Recurrent, etc impact on the Series</remarks>
    public TagWeight? FilterAboveWeight { get; set; }
}
