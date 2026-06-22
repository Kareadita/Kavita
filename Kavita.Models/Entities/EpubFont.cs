using System;
using Kavita.Models.Entities.Interfaces;
using Kavita.Models.Entities.Enums.Font;

namespace Kavita.Models.Entities;

/// <summary>
/// Represents a user provider font to be used in the epub reader
/// </summary>
public class EpubFont: IEntityDate
{
    public int Id { get; set; }

    /// <summary>
    /// Family of the font
    /// </summary>
    public required string Family { get; set; }
    /// <summary>
    /// Name of the font
    /// </summary>
    public required string Name { get; set; }
    /// <summary>
    /// Normalized name for lookups
    /// </summary>
    public required string NormalizedName { get; set; }
    /// <summary>
    /// Filename of the font, stored under <see cref="DirectoryService.EpubFontDirectory"/>
    /// </summary>
    /// <remarks>System provided fonts use an alternative location as they are packaged with the app</remarks>
    public required string FileName { get; set; }
    /// <summary>
    /// Where the font came from
    /// </summary>
    public FontProvider Provider { get; set; }
    /// <summary>
    /// Style for the font, i.e. normal or italic (FontFace font-style values)
    /// </summary>
    public required string Style { get; set; }
    /// <summary>
    /// Supported weight of font, either via a single number for a specific weight or two numbers for a range. e.g. '400' or '100 400'
    /// </summary>
    public required string Weight { get; set; }

    public DateTime Created { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime LastModifiedUtc { get; set; }

    public static readonly string DefaultFont = "Default";
}
