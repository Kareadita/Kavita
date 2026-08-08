using System.Collections.Generic;
using System.Text.Json.Serialization;
using Kavita.Common.Helpers;

namespace Kavita.Models.DTOs.KavitaPlus.Metadata;
#nullable enable

/// <summary>
/// A prioritized set of BCP-47 language codes used to pick which upstream title becomes
/// <c>Series.Name</c> and <c>Series.LocalizedName</c>.
/// </summary>
/// <remarks>
/// Used both for the global setting and for each per-library override. Library association is held by the
/// dictionary key on <see cref="MetadataSettingsDto.LibraryLanguageTitleOverrides"/>, not by this type.
/// </remarks>
public sealed class SeriesNameLanguageDto
{
    /// <summary>
    /// A semicolon separated list of BCP-47 language codes to prioritize for <c>Series.Name</c>.
    /// In case of no matches, the field is left unchanged.
    /// </summary>
    /// <remarks><c>en;ja-Latn</c> translates to trying English first, then romanized Japanese.</remarks>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// A semicolon separated list of BCP-47 language codes to prioritize for <c>Series.LocalizedName</c>.
    /// In case of no matches, the field is left unchanged.
    /// </summary>
    public string LocalizedName { get; set; } = string.Empty;

    /// <summary>
    /// <see cref="Name"/> parsed into priority order. Highest priority first.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<string> NamePriority => LanguageCodeHelper.Split(Name);

    /// <summary>
    /// <see cref="LocalizedName"/> parsed into priority order. Highest priority first.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<string> LocalizedNamePriority => LanguageCodeHelper.Split(LocalizedName);
}
