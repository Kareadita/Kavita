using System.Collections.Generic;
using API.Entities.Enums.Theme;
using API.Services;

namespace API.DTOs.Theme;

/// <summary>
/// Represents a set of css overrides the user can upload to Kavita and will load into webui
/// </summary>
public class SiteThemeDto
{
    public int Id { get; set; }
    /// <summary>
    /// Name of the Theme
    /// </summary>
    public required string Name { get; set; }
    /// <summary>
    /// Normalized name for lookups
    /// </summary>
    public required string NormalizedName { get; set; }
    /// <summary>
    /// File path to the content. Stored under <see cref="DirectoryService.SiteThemeDirectory"/>.
    /// Must be a .css file
    /// </summary>
    public required string FileName { get; set; }
    /// <summary>
    /// Only one theme can have this. Will auto-set this as default for new user accounts
    /// </summary>
    public bool IsDefault { get; set; }
    /// <summary>
    /// Where did the theme come from
    /// </summary>
    public ThemeProvider Provider { get; set; }

    public IList<string> PreviewUrls { get; set; }
    /// <summary>
    /// Information about the theme
    /// </summary>
    public string Description { get; set; }
    /// <summary>
    /// Author of the Theme (only applies to non-system provided themes)
    /// </summary>
    public string Author { get; set; }
    /// <summary>
    /// Last compatible version. System provided will always be most current
    /// </summary>
    public string CompatibleVersion { get; set; }


    public string Selector => "bg-" + Name.ToLower();
}
