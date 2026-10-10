using System.Collections.Generic;

namespace Kavita.Services.Scanner;

public sealed record RetimePlan(IReadOnlyList<RetimeMove> Moves, IReadOnlyList<string> Deletes)
{
    public bool IsEmpty => Moves.Count == 0 && Deletes.Count == 0;
}
