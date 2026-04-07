using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Models.Entities.ReadingLists;

/// <summary>
/// Represents a user-defined string to tag Reading Lists
/// </summary>
[Index(nameof(NormalizedTitle), IsUnique = true)]
public class ReadingListTag
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string NormalizedTitle { get; set; }

    public ICollection<ReadingList> ReadingLists { get; set; }
}
