using System.Collections.Generic;

namespace Kavita.Models.DTOs;

public sealed record DeleteChaptersDto
{
    public IList<int> ChapterIds { get; set; } = default!;
}
