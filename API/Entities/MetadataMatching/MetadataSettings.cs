using System.Collections.Generic;
using System.Linq;
using API.Entities.Enums;

namespace API.Entities;

/// <summary>
/// Represents which field that can be written to as an override when already locked
/// </summary>
public enum MetadataSettingField
{
    Summary = 1,
    PublicationStatus = 2,
    StartDate = 3,
    Genres = 4,
    Tags = 5,
    LocalizedName = 6,
    Covers = 7,
    AgeRating = 8,
    People = 9
}

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
    /// Allow the Summary to be written
    /// </summary>
    public bool EnableSummary { get; set; }
    /// <summary>
    /// Allow Publication status to be derived and updated
    /// </summary>
    public bool EnablePublicationStatus { get; set; }
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
    /// Allow setting the cover image
    /// </summary>
    public bool EnableCoverImage { get; set; }

    // Need to handle the Genre/tags stuff
    public bool EnableGenres { get; set; } = true;
    public bool EnableTags { get; set; } = true;

    /// <summary>
    /// For Authors and Writers, how should names be stored (Exclusively applied for AniList). This does not affect Character names.
    /// </summary>
    public bool FirstLastPeopleNaming { get; set; }

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
}
