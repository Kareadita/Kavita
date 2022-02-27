using System;
using API.Entities.Enums.Theme;
using API.Entities.Interfaces;
using API.Services;

namespace API.Entities;

/// <summary>
/// Represents a set of css overrides the user can upload to Kavita and will load into the book reader. This affects styling of the content, not the UI controls.
/// </summary>
public class BookTheme : IEntityDate
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
    /// File path to the content. Stored under <see cref="DirectoryService.BookThemeDirectory"/>.
    /// Must be a .css file
    /// </summary>
    /// <remarks>System provided themes use an alternative location as they are packaged with the app. This will be empty</remarks>
    public string FileName { get; set; }
    /// <summary>
    /// Only one theme can have this. Will auto-set this as default for new user accounts
    /// </summary>
    public bool IsDefault { get; set; }
    /// <summary>
    /// Internal order for sorting. Not changeable by the user.
    /// </summary>
    public int SortOrder { get; set; }
    /// <summary>
    /// Who owns the theme (Kavita or User)
    /// </summary>
    public ThemeProvider Provider { get; set; }
    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
}
