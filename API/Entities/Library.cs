using System;
using System.Collections.Generic;
using API.Entities.Enums;
using API.Entities.Interfaces;

namespace API.Entities;

public class Library : IEntityDate
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? CoverImage { get; set; }
    public LibraryType Type { get; set; }
    /// <summary>
    /// If Folder Watching is enabled for this library
    /// </summary>
    public bool FolderWatching { get; set; } = true;
    /// <summary>
    /// Include Library series on Dashboard Streams
    /// </summary>
    public bool IncludeInDashboard { get; set; } = true;
    /// <summary>
    /// Include Library series on Recommended Streams
    /// </summary>
    public bool IncludeInRecommended { get; set; } = true;
    /// <summary>
    /// Include library series in Search
    /// </summary>
    public bool IncludeInSearch { get; set; } = true;
    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
    /// <summary>
    /// Last time Library was scanned
    /// </summary>
    /// <remarks>Time stored in UTC</remarks>
    public DateTime LastScanned { get; set; }
    public ICollection<FolderPath> Folders { get; set; } = null!;
    public ICollection<AppUser> AppUsers { get; set; } = null!;
    public ICollection<Series> Series { get; set; } = null!;

}
