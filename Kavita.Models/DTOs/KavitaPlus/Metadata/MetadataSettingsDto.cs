using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Kavita.Models.DTOs.Settings;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.MetadataMatching;
using Kavita.Models.Attributes;
using Kavita.Models.Entities.Enums.KavitaPlus;

namespace Kavita.Models.DTOs.KavitaPlus.Metadata;
#nullable enable

public sealed record MetadataSettingsDto: FieldMappingsDto
{
    /// <summary>
    /// If writing any sort of metadata from upstream (AniList, Hardcover) source is allowed
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Enable processing of metadata outside K+; e.g. disk and API
    /// </summary>
    public bool EnableExtendedMetadataProcessing { get; set; }

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
    /// Optional mappings of strings -> ComicInfo Age Ratings
    /// </summary>
    public Dictionary<string, AgeRating> ExternalAgeRatingMappings { get; set; } = [];

    /// <summary>
    /// The server-wide priority order of BCP-47 language codes used when setting <c>Series.Name</c> and
    /// <c>Series.LocalizedName</c>. In case of no matches, will not change/set.
    /// </summary>
    /// <remarks>en;ja-Latn translates to trying to set <c>Series.Name</c> as English, if no english, use Romanji.</remarks>
    public SeriesNameLanguageDto GlobalLanguageTitleSettings { get; set; } =
        new() { Name = "en", LocalizedName = "ja-Latn" };

    /// <summary>
    /// Per-library overrides of <see cref="GlobalLanguageTitleSettings"/>, keyed by LibraryId.
    /// </summary>
    /// <remarks>An override fully replaces the global list for that library, it does not prepend to it.</remarks>
    public Dictionary<int, SeriesNameLanguageDto> LibraryLanguageTitleOverrides { get; set; } = [];

    /// <summary>
    /// A list of overrides that will enable writing to locked fields
    /// </summary>
    [EnumCollection(typeof(MetadataSettingField))]
    public List<MetadataSettingField> Overrides { get; set; }

    /// <summary>
    /// Which Roles to allow metadata downloading for
    /// </summary>
    [EnumCollection(typeof(PersonRole))]
    public List<PersonRole> PersonRoles { get; set; }

    /// <summary>
    /// Filter Tags above this level. This takes effect after tag mapping.
    /// </summary>
    /// <example>FilterAboveWeight = Recurrent, all tags with weight above Recurrent (incidental, unweighted) will be filtered out after tag mapping step</example>
    /// <remarks>This is only applicable for <see cref="MetadataProvider.Mangabaka"/>. Tag weights are: Core, Defining, Recurrent, etc impact on the Series</remarks>
    [EnumDataType((typeof(TagWeight)))]
    public TagWeight? FilterAboveWeight { get; set; }


    /// <summary>
    /// Override list contains this field
    /// </summary>
    /// <param name="field"></param>
    /// <returns></returns>
    public bool HasOverride(MetadataSettingField field)
    {
        return Overrides.Contains(field);
    }

    /// <summary>
    /// If this Person role is allowed to be written
    /// </summary>
    /// <param name="character"></param>
    /// <returns></returns>
    public bool IsPersonAllowed(PersonRole character)
    {
        return PersonRoles.Contains(character);
    }
}

/// <summary>
/// Decoupled from <see cref="MetadataSettingsDto"/> to allow reuse without requiring the full metadata settings in
/// <see cref="ImportFieldMappingsDto"/>
/// </summary>
public record FieldMappingsDto
{
    /// <summary>
    /// Do not allow any Genre/Tag in this list to be written to Kavita
    /// </summary>
    public List<string> Blacklist { get; set; } = [];

    /// <summary>
    /// Only allow these Tags to be written to Kavita
    /// </summary>
    public List<string> Whitelist { get; set; } = [];

    /// <summary>
    /// Any Genres or Tags that if present, will trigger an Age Rating Override. Highest rating will be prioritized for matching.
    /// </summary>
    public Dictionary<string, AgeRating> AgeRatingMappings { get; set; } = [];

    /// <summary>
    /// A list of rules that allow mapping a genre/tag to another genre/tag
    /// </summary>
    public List<MetadataFieldMappingDto> FieldMappings { get; set; } = [];
}
