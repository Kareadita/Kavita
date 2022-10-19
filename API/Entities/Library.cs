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
    /// <summary>
    /// This is not used, but planned once we build out a Library detail page
    /// </summary>
    [Obsolete("This has never been coded for. Likely we can remove it.")]
    public string CoverImage { get; set; }
    public LibraryType Type { get; set; }
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
