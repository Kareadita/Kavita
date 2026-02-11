using System.Collections.Generic;

namespace Kavita.Models.DTOs.ReadingLists.CBL;


public sealed record CblConflictQuestion
{
    public string SeriesName { get; set; }
    public IList<int> LibrariesIds { get; set; }
}
