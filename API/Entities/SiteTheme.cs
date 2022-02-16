﻿using System;
using System.Collections.Generic;
using API.Entities.Enums.Theme;
using API.Entities.Interfaces;
using API.Services;

namespace API.Entities;
/// <summary>
/// Represents a set of css overrides the user can upload to Kavita and will load into webui
/// </summary>
public class SiteTheme : IEntityDate
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
    /// File path to the content. Stored under <see cref="DirectoryService.SiteThemeDirectory"/>.
    /// Must be a .css file
    /// </summary>
    public string FileName { get; set; }
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
