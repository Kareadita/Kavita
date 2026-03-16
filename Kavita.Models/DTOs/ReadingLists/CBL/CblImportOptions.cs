using System.Collections.Generic;

namespace Kavita.Models.DTOs.ReadingLists.CBL;

public record CblImportOptions
{
    /// <summary>
    /// Weighs ComicVine Matching higher
    /// </summary>
    public bool PreferComicVineMatching { get; set; }
    /// <summary>
    /// Libraries to search against. If empty, will include all
    /// </summary>
    public IList<int> ApplicableLibraries { get; set; }

}
