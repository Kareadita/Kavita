using System;
using API.Entities.Enums.Theme;
using API.Services;

namespace API.DTOs.Theme;

/// <summary>
/// Represents a set of css overrides the user can upload to Kavita and will load into the book reader. This affects styling of the content, not the UI controls.
/// </summary>
public class BookThemeDto
{
    public int Id { get; set; }
    /// <summary>
    /// Name of the Theme
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// Normalized name for lookups
    /// </summary>
    public string NormalizedName { get; set; }
    /// <summary>
    /// A color hex that will be used to render the theme to the user. For example, "light" theme might have #FFFFFF
    /// </summary>
    public string ColorHash { get; set; }
    /// <summary>
    /// File path to the content.
    /// Must be a .css file
    /// </summary>
    /// <remarks>System provided themes use an alternative location as they are packaged with the app. This will be empty</remarks>
    public string FilePath { get; set; }
    /// <summary>
    /// Only one theme can have this. Will auto-set this as default for new user accounts
    /// </summary>
    public bool IsDefault { get; set; }
    /// <summary>
    /// Is this theme providing dark mode to the reader aka Should we style the reader controls to be dark mode
    /// </summary>
    public bool IsDarkTheme { get; set; }
    /// <summary>
    /// Where did the theme come from
    /// </summary>
    public ThemeProvider Provider { get; set; }
    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
    public string Selector => "brtheme-" + Name.ToLower();
}
