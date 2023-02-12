using System.Collections.Generic;

namespace API.DTOs.ReadingLists.CBL;


public class CblConflictQuestion
{
    public string SeriesName { get; set; }
    public IList<int> LibrariesIds { get; set; }
}
