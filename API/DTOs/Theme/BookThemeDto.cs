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
    /// Contents of the theme.
    /// </summary>
    public string Contents { get; set; }
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
}
