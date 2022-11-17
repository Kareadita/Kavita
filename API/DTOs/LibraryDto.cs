using System;
using System.Collections.Generic;
using API.Entities.Enums;

namespace API.DTOs;

public class LibraryDto
{
    public int Id { get; init; }
    public string Name { get; init; }
    /// <summary>
    /// Last time Library was scanned
    /// </summary>
    public DateTime LastScanned { get; init; }
    public LibraryType Type { get; init; }
    /// <summary>
    /// An optional Cover Image or null
    /// </summary>
    public string CoverImage { get; init; }
    public ICollection<string> Folders { get; init; }
}
