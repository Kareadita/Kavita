namespace Kavita.Models.Entities.MetadataMatching;

/// <summary>
/// A prioritized set of BCP-47 language codes used when picking which upstream title becomes
/// <c>Series.Name</c> and <c>Series.LocalizedName</c>.
/// </summary>
/// <remarks>
/// Not a table. This only ever lives inside the JSON column on
/// <see cref="MetadataSettings.LibraryLanguageTitleOverrides"/>, where the LibraryId is the dictionary key.
/// </remarks>
public class SeriesNameLanguage
{
    /// <summary>
    /// A semicolon separated list of BCP-47 language codes to prioritize for <c>Series.Name</c>
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// A semicolon separated list of BCP-47 language codes to prioritize for <c>Series.LocalizedName</c>
    /// </summary>
    public string LocalizedName { get; set; } = string.Empty;
}
