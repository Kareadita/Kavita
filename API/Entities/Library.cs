using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using API.Entities.Enums;
using API.Entities.Interfaces;

namespace API.Entities;

public class Library : IEntityDate
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string CoverImage { get; set; } = null;
    public LibraryType Type { get; set; }
    /// <summary>
    /// If Folder Watching is enabled for this library
    /// </summary>
    //public bool FolderWatching { get; set; } = true;
    /// <summary>
    /// Include Library series on Dashboard Streams
    /// </summary>
   // public bool IncludeInDashboard { get; set; } = true;
    /// <summary>
    /// Include Library series on Recommended Streams
    /// </summary>
    //public bool IncludeInRecommended { get; set; } = true;
    /// <summary>
    /// When performing a library scan, allow Image format Series
    /// </summary>
    //public bool AllowImageSeries { get; set; } = true;
    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
    /// <summary>
    /// Last time Library was scanned
    /// </summary>
    /// <remarks>Time stored in UTC</remarks>
    public DateTime LastScanned { get; set; }
    public ICollection<FolderPath> Folders { get; set; }
    public ICollection<AppUser> AppUsers { get; set; }
    public ICollection<Series> Series { get; set; }

}
