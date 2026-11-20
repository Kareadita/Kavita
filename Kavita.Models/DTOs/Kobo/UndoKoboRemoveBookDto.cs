using System.Collections.Generic;

namespace Kavita.Models.DTOs.Kobo;

public sealed record UndoKoboRemoveBookDto
{
    public int SyncPointId { get; set; }
    public List<int> UndoBookIds { get; set; }
}
