using System.Collections.Generic;

namespace API.DTOs.ReadingLists.CBL;

/// <summary>
/// Represents conflicts and the user's answers
/// </summary>
public class CblConflictsDto
{
    public ICollection<CblConflictQuestion> ConflictAnswers { get; set; }
}

public class CblConflictQuestion
{
    public string SeriesName { get; set; }
    public string Number { get; set; }
    public string Volume { get; set; }
}

public class CblConflictAnswersDto
{
    public ICollection<CblConflictAnswer> ConflictAnswers { get; set; }
}

public class CblConflictAnswer
{
    public string SeriesName { get; set; }
    public string Number { get; set; }
    public string Volume { get; set; }
    public int LibraryId { get; set; }
}
