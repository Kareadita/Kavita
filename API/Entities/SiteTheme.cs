using System;
using API.Entities.Enums.Theme;
using API.Entities.Interfaces;
using API.Services;

namespace API.Entities;
/// <summary>
/// Represents a set of css overrides the user can upload to Kavita and will load into webui
/// </summary>
public class SiteTheme : IEntityDate, ITheme
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
    /// <remarks>System provided themes use an alternative location as they are packaged with the app</remarks>
    public required string FileName { get; set; }
    /// <summary>
    /// Only one theme can have this. Will auto-set this as default for new user accounts
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Where did the theme come from
    /// </summary>
    public ThemeProvider Provider { get; set; }
    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastModifiedUtc { get; set; }

    #region ThemeBrowser

    /// <summary>
    /// The Url on the repo to download the file from
    /// </summary>
    public string? GitHubPath { get; set; }
    /// <summary>
    /// Hash of the Css File
    /// </summary>
    public string? ShaHash { get; set; }
    /// <summary>
    /// Pipe (|) separated urls of the images. Empty string if
    /// </summary>
    public string PreviewUrls { get; set; }
    // /// <summary>
    // /// A description about the theme
    // /// </summary>
    public string Description { get; set; }
    // /// <summary>
    // /// Author of the Theme
    // /// </summary>
    public string Author { get; set; }
    public string CompatibleVersion { get; set; }

    #endregion
}
